using System.Diagnostics;
using System.Diagnostics.Metrics;
using ModulithFoundry.Modules.Purchasing.Composition;

namespace ModulithFoundry.Modules.Purchasing.Messaging;

internal sealed class PurchasingMessagingMetrics
{
    private Observation? observation;
    private ProjectionObservation? projection;
    private readonly Counter<long> projectionObservationFailures;
    private readonly Counter<long> bootstrapFailures;
    private readonly Counter<long> projectionProcessingFailures;
    private readonly Counter<long> reconciliationFailures;
    private readonly Counter<long> observationFailures;
    private readonly Counter<long> dispatchFailures;
    private readonly Counter<long> relayFailures;
    private readonly Counter<long> inboxDuplicates;

    public PurchasingMessagingMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(PurchasingMessaging.MeterName);
        projectionProcessingFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.stock_item_projection.processing_failures",
            "{failure}",
            "Failed typed reference-adapter attempts, excluding requested shutdown; broker retries can count again."
        );
        reconciliationFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.stock_item_projection.reconciliation_failures",
            "{failure}",
            "Failed explicit comparison or repair attempts, excluding requested cancellation."
        );
        bootstrapFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.stock_item_projection.bootstrap_failures",
            "{failure}",
            "Failed hosted bootstrap attempts, excluding requested shutdown; retries can count again."
        );
        projectionObservationFailures = meter.CreateCounter<long>(
            "modulith_foundry.purchasing.stock_item_projection.observation_failures",
            "{failure}",
            "Failed bootstrap-state observations, excluding requested shutdown; not projection processing failures."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.purchasing.stock_item_projection.ready",
            ProjectionReady,
            "{state}",
            "Bootstrap readiness at the last successful observation; not tail freshness or reconciliation correctness."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.purchasing.stock_item_projection.sample_age",
            ProjectionSampleAge,
            "s",
            "Elapsed time since the last successful bootstrap-state observation."
        );
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

    internal void ObserveProjection(bool ready) =>
        Volatile.Write(ref projection, new(ready, Stopwatch.GetTimestamp()));

    internal void ProjectionObservationFailed() => projectionObservationFailures.Add(1);

    internal void BootstrapFailed() => bootstrapFailures.Add(1);

    internal void ProjectionProcessingFailed() => projectionProcessingFailures.Add(1);

    internal void ReconciliationFailed() => reconciliationFailures.Add(1);

    private IEnumerable<Measurement<long>> ProjectionReady()
    {
        var current = Volatile.Read(ref projection);
        return current is null ? [] : [new(current.Ready ? 1 : 0)];
    }

    private IEnumerable<Measurement<double>> ProjectionSampleAge()
    {
        var current = Volatile.Read(ref projection);
        return current is null
            ? []
            : [new(Stopwatch.GetElapsedTime(current.Timestamp).TotalSeconds)];
    }

    private sealed record ProjectionObservation(bool Ready, long Timestamp);

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
