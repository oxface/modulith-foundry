using Microsoft.Extensions.Logging;
using Rebus.Messages;
using Rebus.Pipeline;

namespace ModulithFoundry.BrokerReceiver;

// Pause actual empty-receipt SQL reads before either process can commit its first delivery.
internal sealed class ReceiptReadCheckpoint : ILoggerProvider
{
    private readonly ManualResetEventSlim release = new(false);

    internal void Release() => release.Set();

    public ILogger CreateLogger(string categoryName) => new CheckpointLogger(this, categoryName);

    public void Dispose() => release.Dispose();

    private sealed class CheckpointLogger(ReceiptReadCheckpoint checkpoint, string category)
        : ILogger
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
            string sql = formatter(state, exception);
            if (
                !sql.Contains("FROM inventory.inbox_receipts", StringComparison.Ordinal)
                && !sql.Contains("FROM sales.inbox_receipts", StringComparison.Ordinal)
            )
                return;
            Console.WriteLine($"lookup:{MessageContext.Current.Headers[Headers.MessageId]}");
            checkpoint.release.Wait();
        }
    }
}
