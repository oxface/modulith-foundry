using System.Diagnostics;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace ModulithFoundry.TopologyTests;

public sealed partial class LocalRuntimeTests
{
    private static async Task<HttpResponseMessage> CancelAcrossApiCrashAsync(
        DistributedApplication app,
        HttpClient client,
        string csrf,
        string orderUrl,
        string[] replicas,
        bool afterCommit,
        CancellationToken cancellationToken
    )
    {
        await using (
            var checkpoint = await CancellationIngressCheckpoint.CreateAsync(
                Assert.IsType<string>(
                    await app.GetConnectionStringAsync("database", cancellationToken)
                ),
                afterCommit,
                cancellationToken
            )
        )
        {
            Task<HttpResponseMessage> lostResponse = SendCommandAsync(
                client,
                HttpMethod.Post,
                orderUrl + "/cancel",
                csrf,
                new { expectedVersion = 3, reason = "Customer withdrew" },
                cancellationToken
            );
            await checkpoint.WaitAsync(afterCommit, cancellationToken);
            // Kill only the actual API processes reported by this disposable AppHost. No machine-wide cleanup.
            foreach (string replica in replicas)
            {
                Assert.True(app.ResourceNotifications.TryGetCurrentState(replica, out var state));
                Assert.Equal("api", state.Resource.Name);
                Assert.Equal(KnownResourceStates.Running, state.Snapshot.State?.Text);
                int pid = int.Parse(
                    Assert
                        .Single(
                            state.Snapshot.Properties,
                            property => property.Name == "executable.pid"
                        )
                        .Value!.ToString()!,
                    System.Globalization.CultureInfo.InvariantCulture
                );
                Assert.NotEqual(Environment.ProcessId, pid);
                using var process = Process.GetProcessById(pid);
                Assert.False(process.HasExited);
                DateTime started = Assert.IsType<DateTime>(state.Snapshot.StartTimeStamp);
                Assert.InRange(
                    Math.Abs(
                        (
                            process.StartTime.ToUniversalTime() - started.ToUniversalTime()
                        ).TotalSeconds
                    ),
                    0,
                    2
                );
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }
            if (!afterCommit)
                await checkpoint.WaitForBlockedClientExitAsync(cancellationToken);
            try
            {
                // Deliberately discard any response: the client must inspect retained state, not infer failure.
                using var ignored = await lostResponse;
            }
            catch (HttpRequestException)
            {
                // Abrupt transport loss is expected; it says nothing about the committed business result.
            }
        }
        foreach (string replica in replicas)
        {
            await WaitForApiReplicaExitAsync(app, replica, cancellationToken);
            ExecuteCommandResult started = await app.ResourceCommands.ExecuteCommandAsync(
                replica,
                KnownResourceCommands.StartCommand,
                cancellationToken
            );
            Assert.True(started.Success, started.Message);
        }
        await WaitForApiReplicasAsync(app, replicas.Length, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("api", cancellationToken);
        using var current = await client.GetAsync(orderUrl, cancellationToken);
        current.EnsureSuccessStatusCode();
        using var stateJson = JsonDocument.Parse(
            await current.Content.ReadAsStringAsync(cancellationToken)
        );
        Assert.Equal(
            afterCommit ? "cancelled" : "approved",
            stateJson.RootElement.GetProperty("status").GetString()
        );
        // Retry the original request after inspection: cancellation retains its first intent/timestamp.
        return await SendCommandAsync(
            client,
            HttpMethod.Post,
            orderUrl + "/cancel",
            csrf,
            new { expectedVersion = 3, reason = "Customer withdrew" },
            cancellationToken
        );
    }
}
