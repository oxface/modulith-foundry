using System.Diagnostics;
using System.Diagnostics.Metrics;
using ModulithFoundry.Modules.Sales.Composition;

namespace ModulithFoundry.Modules.Sales.Messaging;

internal sealed class SalesMessagingMetrics
{
    private Observation? observation;
    private ProcessObservation? processObservation;
    private readonly Counter<long> observationFailures;
    private readonly Counter<long> dispatchFailures;
    private readonly Counter<long> relayFailures;
    private readonly Counter<long> inboxDuplicates;
    private readonly Counter<long> processDispatchFailures;
    private readonly Counter<long> processObservationFailures;

    public SalesMessagingMetrics(IMeterFactory meters)
    {
        var meter = meters.Create(SalesMessaging.MeterName);
        processObservationFailures = meter.CreateCounter<long>(
            "modulith_foundry.sales.fulfilment.observation_failures",
            "{failure}",
            "Failed process observations, excluding requested shutdown; not business failures."
        );
        processDispatchFailures = meter.CreateCounter<long>(
            "modulith_foundry.sales.fulfilment.dispatch_failures",
            "{failure}",
            "Failed per-process dispatch attempts; not a durable failure total or proof of a failed remote effect."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.sales.fulfilment.unsettled",
            ProcessUnsettled,
            "{process}",
            "Processes not reserved or compensated; includes legitimate waits and operator attention."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.sales.fulfilment.oldest_age",
            ProcessOldestAge,
            "s",
            "Creation age of the oldest unsettled process at the last successful observation; not a failure deadline."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.sales.fulfilment.sample_age",
            ProcessSampleAge,
            "s",
            "Elapsed time since the last successful process observation, independent of outbox freshness."
        );
        inboxDuplicates = meter.CreateCounter<long>(
            "modulith_foundry.sales.inbox.duplicates",
            "{delivery}",
            "Validated deliveries suppressed by an existing inbox receipt; not semantic-operation duplicates."
        );
        dispatchFailures = meter.CreateCounter<long>(
            "modulith_foundry.sales.outbox.dispatch_failures",
            "{failure}",
            "Failed serialization, publication or dispatch-mark attempts; publication may have succeeded."
        );
        relayFailures = meter.CreateCounter<long>(
            "modulith_foundry.sales.outbox.relay_failures",
            "{failure}",
            "Failed claim/relay iterations, excluding requested shutdown."
        );
        observationFailures = meter.CreateCounter<long>(
            "modulith_foundry.sales.outbox.observation_failures",
            "{failure}",
            "Failed backlog observations; not business or dispatch failures."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.sales.outbox.pending",
            Pending,
            "{message}",
            "Committed unpublished rows, including leased and delayed work."
        );
        meter.CreateObservableGauge<long>(
            "modulith_foundry.sales.outbox.expired_leases",
            ExpiredLeases,
            "{message}",
            "Unpublished rows with expired leases, including delayed work; not proof that a publisher has stopped."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.sales.outbox.oldest_age",
            OldestAge,
            "s",
            "Oldest unpublished row age at the last successful observation."
        );
        meter.CreateObservableGauge<double>(
            "modulith_foundry.sales.outbox.sample_age",
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

    private IEnumerable<Measurement<long>> ProcessUnsettled()
    {
        var current = Volatile.Read(ref processObservation);
        return current is null ? [] : [new(current.Unsettled)];
    }

    private IEnumerable<Measurement<double>> ProcessOldestAge()
    {
        var current = Volatile.Read(ref processObservation);
        return current is null ? [] : [new(current.OldestAge)];
    }

    private IEnumerable<Measurement<double>> ProcessSampleAge()
    {
        var current = Volatile.Read(ref processObservation);
        return current is null
            ? []
            : [new(Stopwatch.GetElapsedTime(current.Timestamp).TotalSeconds)];
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

    internal void ProcessDispatchFailed() => processDispatchFailures.Add(1);

    internal void ProcessObservationFailed() => processObservationFailures.Add(1);

    internal void ObserveProcesses(long unsettled, double oldestAge) =>
        Volatile.Write(ref processObservation, new(unsettled, oldestAge, Stopwatch.GetTimestamp()));

    private sealed record ProcessObservation(long Unsettled, double OldestAge, long Timestamp);

    private sealed record Observation(
        long Pending,
        double OldestAge,
        long ExpiredLeases,
        long Timestamp
    );
}
