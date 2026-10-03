using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ModulithFoundry.BrokerTests;

// Broker-owned operational observations only; never a substitute for module state assertions.
internal sealed class BrokerQueueProbe : IDisposable
{
    private readonly HttpClient client;

    internal BrokerQueueProbe(Uri management, string connection)
    {
        var credentials = new Uri(connection).UserInfo.Split(':', 2);
        string basic = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(
                $"{Uri.UnescapeDataString(credentials[0])}:{Uri.UnescapeDataString(credentials[1])}"
            )
        );
        client = new HttpClient { BaseAddress = management };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
    }

    internal async Task<QueueObservation> WaitAsync(
        string queue,
        Func<QueueObservation, bool> matches,
        CancellationToken cancellationToken,
        bool missingIsEmpty = false
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        while (true)
        {
            using var response = await client.GetAsync(
                $"api/queues/%2F/{Uri.EscapeDataString(queue)}",
                timeout.Token
            );
            if (response.StatusCode == HttpStatusCode.NotFound && missingIsEmpty)
            {
                var absent = new QueueObservation(0, 0);
                if (matches(absent))
                    return absent;
            }
            else if (response.StatusCode != HttpStatusCode.NotFound)
            {
                response.EnsureSuccessStatusCode();
                using var json = JsonDocument.Parse(
                    await response.Content.ReadAsStringAsync(timeout.Token)
                );
                var root = json.RootElement;
                // A newly declared queue may not have its first management statistics sample yet.
                if (
                    root.TryGetProperty("messages_ready", out var ready)
                    && root.TryGetProperty("messages_unacknowledged", out var unacknowledged)
                )
                {
                    var observation = new QueueObservation(
                        ready.GetInt64(),
                        unacknowledged.GetInt64()
                    );
                    if (matches(observation))
                        return observation;
                }
            }
            await Task.Delay(100, timeout.Token);
        }
    }

    public void Dispose() => client.Dispose();

    internal sealed record QueueObservation(long Ready, long Unacknowledged);
}
