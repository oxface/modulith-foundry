using System.Diagnostics;
using System.Diagnostics.Metrics;
using ModulithFoundry.Modules.Purchasing.Composition;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class PurchasingMessagingMetrics
{
    private Observation? observation;
    private readonly Counter<long> observationFailures;
    private readonly Counter<long> dispatchFailures;
    private readonly Counter<long> relayFailures;
    private readonly Counter<long> inboxDuplicates;

    public PurchasingMessagingMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(PurchasingMessaging.MeterName);
        inboxDuplicates = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.inbox.duplicates",
            "{delivery}",
            "Validated deliveries suppressed by an existing inbox receipt; not semantic-operation duplicates."
        );
        dispatchFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.outbox.dispatch_failures",
            "{failure}",
            "Failed serialization, publication or dispatch-mark attempts; publication may have succeeded."
        );
        relayFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.outbox.relay_failures",
            "{failure}",
            "Failed claim/relay iterations, excluding requested shutdown."
        );
        observationFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.outbox.observation_failures",
            "{failure}",
            "Failed backlog observations; not business or dispatch failures."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.purchasing.outbox.pending",
            Pending,
            "{message}",
            "Committed unpublished rows, including leased and delayed work."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.purchasing.outbox.expired_leases",
            ExpiredLeases,
            "{message}",
            "Unpublished rows with expired leases, including delayed work; not proof that a publisher has stopped."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.purchasing.outbox.oldest_age",
            OldestAge,
            "s",
            "Oldest unpublished row age at the last successful observation."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.purchasing.outbox.sample_age",
            SampleAge,
            "s",
            "Elapsed time since the last successful database observation."
        );
    }

    internal void Observe(long pending, double oldestAge, long expiredLeases) =>
        Volatile.Write(
            ref observation,
            new(pending, oldestAge, expiredLeases, Stopwatch.GetTimestamp())
        );

    private IEnumerable<Measurement<long>> ExpiredLeases()
    {
        var current = Volatile.Read(ref observation);
        return current is null ? [] : [new(current.ExpiredLeases)];
    }

    private IEnumerable<Measurement<long>> Pending()
    {
        var current = Volatile.Read(ref observation);
        return current is null ? [] : [new(current.Pending)];
    }

    private IEnumerable<Measurement<double>> OldestAge()
    {
        var current = Volatile.Read(ref observation);
        return current is null ? [] : [new(current.OldestAge)];
    }

    private IEnumerable<Measurement<double>> SampleAge()
    {
        var current = Volatile.Read(ref observation);
        return current is null
            ? []
            : [new(Stopwatch.GetElapsedTime(current.Timestamp).TotalSeconds)];
    }

    internal void ObservationFailed() => observationFailures.Add(1);

    internal void DispatchFailed() => dispatchFailures.Add(1);

    internal void RelayFailed() => relayFailures.Add(1);

    internal void InboxDuplicate() => inboxDuplicates.Add(1);

    private sealed record Observation(
        long Pending,
        double OldestAge,
        long ExpiredLeases,
        long Timestamp
    );
}
