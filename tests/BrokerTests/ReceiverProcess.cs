using System.Diagnostics;
using System.Threading.Channels;
using ModulithFoundry.BrokerReceiver;
using Npgsql;

namespace ModulithFoundry.BrokerTests;

internal sealed class ReceiverProcess : IAsyncDisposable
{
    private readonly Process process;
    private readonly Channel<string> signals = Channel.CreateUnbounded<string>();
    private readonly Task output;
    private readonly Task<string> errors;
    private readonly HashSet<string> pendingSignals = [];

    private ReceiverProcess(Process process, string applicationName)
    {
        this.process = process;
        ApplicationName = applicationName;
        errors = process.StandardError.ReadToEndAsync();
        output = ReadSignalsAsync();
    }

    internal string ApplicationName { get; }

    internal static async Task<ReceiverProcess> StartAsync(
        ReservationFixture fixture,
        bool pauseAfterCommit = false
    ) =>
        await StartAsync(
            fixture.DatabaseConnectionString,
            fixture.BrokerConnectionString,
            "inventory",
            pauseAfterCommit,
            false,
            fixture.CancellationToken
        );

    internal static Task<ReceiverProcess> StartPurchasingAsync(
        StockItemBootstrapFixture fixture,
        bool pauseAfterCommit = false,
        bool pauseSnapshot = false
    ) =>
        StartAsync(
            fixture.DatabaseConnectionString,
            fixture.BrokerConnectionString,
            "purchasing",
            pauseAfterCommit,
            pauseSnapshot,
            fixture.CancellationToken
        );

    internal static Task<ReceiverProcess> StartSalesAsync(
        SalesFulfilmentFixture fixture,
        bool pauseAfterCommit = false
    ) =>
        StartAsync(
            fixture.DatabaseConnectionString,
            fixture.BrokerConnectionString,
            "sales",
            pauseAfterCommit,
            false,
            fixture.CancellationToken
        );

    private static async Task<ReceiverProcess> StartAsync(
        string databaseConnection,
        string brokerConnection,
        string module,
        bool pauseAfterCommit,
        bool pauseSnapshot,
        CancellationToken cancellationToken
    )
    {
        string applicationName = $"receiver-test-{Guid.NewGuid():N}";
        var database = new NpgsqlConnectionStringBuilder(databaseConnection)
        {
            ApplicationName = applicationName,
            // Do not release a test SQL barrier before PostgreSQL detects the dead client.
            Options = "-c client_connection_check_interval=100ms",
        };
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("exec");
        // The independently hosted child needs its own complete dependency set. The test host's
        // framework references may otherwise prune assemblies required by the child's deps.json.
        start.ArgumentList.Add(
            Path.Combine(
                AppContext.BaseDirectory,
                "receiver",
                Path.GetFileName(typeof(ReceiverProcessMarker).Assembly.Location)
            )
        );
        // Credentials travel through the private child environment, never CLI arguments or signals.
        start.Environment["ConnectionStrings__database"] = database.ConnectionString;
        start.Environment["ConnectionStrings__rabbitmq"] = brokerConnection;
        start.Environment["ReceiverTest__Module"] = module;
        start.Environment["ReceiverTest__PauseSnapshot"] = pauseSnapshot ? "true" : "false";
        start.Environment["ReceiverTest__PauseAfterCommit"] = pauseAfterCommit ? "true" : "false";
        var child = new ReceiverProcess(
            Process.Start(start)
                ?? throw new InvalidOperationException("Could not start the test receiver."),
            applicationName
        );
        try
        {
            await child.WaitForSignalAsync("ready", cancellationToken);
            return child;
        }
        catch
        {
            await child.DisposeAsync();
            throw;
        }
    }

    internal async Task WaitForSignalAsync(string signal, CancellationToken cancellationToken)
    {
        if (pendingSignals.Remove(signal))
            return;
        try
        {
            while (true)
            {
                string received = await signals.Reader.ReadAsync(cancellationToken);
                if (received == signal)
                    return;
                pendingSignals.Add(received);
            }
        }
        catch (ChannelClosedException exception)
        {
            string diagnostics = await errors.WaitAsync(
                TimeSpan.FromSeconds(10),
                cancellationToken
            );
            throw new InvalidOperationException(
                $"Receiver exited before '{signal}'. {diagnostics}",
                exception
            );
        }
    }

    private async Task ReadSignalsAsync()
    {
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
                await signals.Writer.WriteAsync(line);
        }
        finally
        {
            signals.Writer.TryComplete();
        }
    }

    internal async Task KillAsync()
    {
        Assert.False(
            process.HasExited,
            "The receiver must still be running at the controlled crash boundary."
        );
        process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await process.WaitForExitAsync(timeout.Token);
        Assert.NotEqual(0, process.ExitCode);
    }

    public async ValueTask DisposeAsync()
    {
        if (!process.HasExited)
            process.Kill(entireProcessTree: true);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(output, errors).WaitAsync(timeout.Token);
        }
        finally
        {
            process.Dispose();
        }
    }
}
