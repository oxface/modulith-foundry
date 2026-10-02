using Microsoft.Extensions.Logging;

namespace ModulithFoundry.BrokerReceiver;

// Test-only pause after PostgreSQL establishes the real export snapshot.
internal sealed class SnapshotCheckpoint : ILoggerProvider
{
    private readonly ManualResetEventSlim release = new(false);

    public ILogger CreateLogger(string categoryName) => new CheckpointLogger(this, categoryName);

    public void Dispose() => release.Dispose();

    private sealed class CheckpointLogger(SnapshotCheckpoint checkpoint, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (category != "Microsoft.EntityFrameworkCore.Database.Command" || eventId.Id != 20101)
                return;
            string message = formatter(state, exception);
            if (
                !message.Contains(
                    "FROM inventory.stock_item_reference_feed",
                    StringComparison.Ordinal
                ) || message.Contains("FOR UPDATE", StringComparison.Ordinal)
            )
                return;
            Console.WriteLine("snapshot");
            checkpoint.release.Wait();
        }
    }
}
