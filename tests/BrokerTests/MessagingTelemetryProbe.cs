using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace ModulithFoundry.BrokerTests;

internal sealed class MessagingTelemetryProbe : ILoggerProvider
{
    private readonly MeterListener listener = new();
    private readonly ConcurrentDictionary<string, double> gauges = new();
    private readonly ConcurrentDictionary<string, double> counters = new();
    private readonly ConcurrentQueue<string> logs = new();
    private readonly ConcurrentQueue<KeyValuePair<string, object?>> tags = new();

    internal IReadOnlyCollection<string> Logs => logs.ToArray();
    internal IReadOnlyCollection<KeyValuePair<string, object?>> Tags => tags.ToArray();

    internal void Observe(IMeterFactory scope)
    {
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (
                ReferenceEquals(instrument.Meter.Scope, scope)
                && instrument.Name.StartsWith("modulith_foundry.", StringComparison.Ordinal)
            )
                meterListener.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>(
            (instrument, value, labels, _) => Record(instrument, value, labels)
        );
        listener.SetMeasurementEventCallback<double>(
            (instrument, value, labels, _) => Record(instrument, value, labels)
        );
        listener.Start();
    }

    private void Record(
        Instrument instrument,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> labels
    )
    {
        foreach (var tag in labels)
            tags.Enqueue(tag);
        if (instrument.IsObservable)
            gauges[instrument.Name] = value;
        else
            counters.AddOrUpdate(instrument.Name, value, (_, previous) => previous + value);
    }

    internal async Task<double> WaitGaugeAsync(
        string name,
        Func<double, bool> matches,
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (true)
            {
                listener.RecordObservableInstruments();
                if (gauges.TryGetValue(name, out var value) && matches(value))
                    return value;
                await Task.Delay(50, timeout.Token);
            }
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Expected telemetry was not observed: {name}; latest value: {gauges.GetValueOrDefault(name)}.",
                exception
            );
        }
    }

    internal double Counter(string name) => counters.GetValueOrDefault(name);

    internal async Task WaitCounterAsync(
        string name,
        double minimum,
        CancellationToken cancellationToken
    )
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        try
        {
            while (Counter(name) < minimum)
                await Task.Delay(50, timeout.Token);
        }
        catch (OperationCanceledException exception)
            when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Expected counter was not observed: {name}; latest value: {Counter(name)}.",
                exception
            );
        }
    }

    public ILogger CreateLogger(string categoryName) => new CapturedLogger(categoryName, logs);

    public void Dispose() => listener.Dispose();

    private sealed class CapturedLogger(string category, ConcurrentQueue<string> records) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            category.StartsWith("ModulithFoundry.Modules.", StringComparison.Ordinal)
            && (
                category.Contains(".Messaging.", StringComparison.Ordinal)
                || category.EndsWith(".PendingFulfilmentDispatcher", StringComparison.Ordinal)
            );

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (IsEnabled(logLevel))
                records.Enqueue(
                    formatter(state, exception) + (exception is null ? "" : exception.ToString())
                );
        }
    }
}
