# Workflow operational proof

The remaining 5.6b diagnostics/recovery and 5.6c failure cases are one workstream. Its first review-sized checkpoint, **5.6bc1**, adds Sales process observations and pairs them with damaged-process isolation, broker interruption and orderly pending-work shutdown proofs. It does not close the entire workstream.

## Process signals

These instruments use the existing `ModulithFoundry.Sales.Messaging` meter, with the prefix `modulith_foundry.sales.fulfilment`. They have no metric labels containing tenant, process, message, actor or payload data.

| Suffix | Signal | Meaning |
| --- | --- | --- |
| `unsettled` | Gauge/processes | All processes whose status is neither Reserved nor Compensated. Includes legitimate waits, shortages and AttentionRequired; not a failed-process count. |
| `oldest_age` | Gauge/seconds | Creation age of the oldest such process at the last successful sample, zero for an observed empty set. Not time since progress, a response deadline or permission to compensate. |
| `sample_age` | Gauge/seconds | Monotonic age of the last successful process observation, independent of outbox sampling. |
| `observation_failures` | Counter/failures | Failed process samples, excluding requested shutdown. |
| `dispatch_failures` | Counter/failures | Failed attempts to queue a particular process's pending reservation/replenishment intent. Retries can count again; not a durable total or evidence of a failed remote effect. |

Sales owns the status interpretation and SQL. A separate read-only worker samples its own schema across Organizations, using PostgreSQL time, a five-second query timeout and a five-second pause. Immutable samples are read from memory by metric callbacks. Unknown startup is absent; failure retains the last sample rather than substituting zero. The monitor neither mutates nor loads business aggregates and does not join another module's tables. Database/pool capacity is still shared.

Replica observations cover the same durable processes: do not sum these gauges across instances. Read their own freshness before interpreting count/age. Terminal-state filtering scans historical process rows; measure workload before choosing indexes or polling changes.

The pending dispatcher already isolates each process in a fresh scope and continues after a non-cancellation fault. Its module-owned logs now retain process identity and exception type without exporting exception messages or stacks. The outer scan log uses exception type too. This is not an audit of EF, Rebus or every other application log.

## Diagnose and repair

1. Missing/stale process observations require source/worker diagnosis, not replay. A fresh outbox sample does not imply a fresh process sample.
2. Inspect the owning Sales fulfilment Contract with authorized Organization context. PendingDispatch may legitimately mean the sample's MAIN location is absent; restore valid setup rather than modifying process rows. AwaitingReservations/AwaitingReplenishment/CompensationPending may indicate a missing outcome, not a failed remote effect.
3. Correlate safe process logs and named module error queues. Preserve original delivery/business identities. After repairing the cause, use supported destination redrive or [Inventory retained-outcome recovery](message-delivery-recovery.md); never clear receipts/tombstones to force processing. No arbitrary Sales retry or public maintenance route is introduced here.
4. After broker restoration, allow connection/consumer recovery and the existing durable relay schedule. The pinned [Rebus.RabbitMq 10.1.1 consumer implementation](https://github.com/rebus-org/Rebus.RabbitMq/blob/10.1.1/Rebus.RabbitMq/RabbitMq/RabbitMqTransport.cs#L760-L802) waits one minute after failed consumer initialization. An early completed publish mark is not proof of receiving-handler completion. RabbitMQ likewise distinguishes [publisher confirms and consumer acknowledgements](https://www.rabbitmq.com/docs/confirms).
5. Verify completion through process/activity and Inventory state Contracts. Do not manufacture a new operation because an observation is old.

## Evidence and template/library findings

`WorkflowOperationalTests` runs in the existing PostgreSQL/RabbitMQ Broker CI lane, using existing product Contracts and native metrics/logs. SQL only arranges table/trigger faults. Testcontainers owns broker stop/start; no container CLI, fake transport or production fault switch is added.

- A trigger blocks one process's commit. A different approved process completes; the blocked process remains pending. Removing the trigger lets the same process complete with one reservation activity and the expected stock effect. Its module log includes identity without injected private exception text.
- Pending work reports count/creation age; reservation settlement clears both. Unavailable process source retains the known count and exposes increasing sample age/failure count while independent outbox observations remain fresh. Restoring the source resumes observation without process changes.
- With the real broker stopped, approval still commits recoverable intent and no stock reservation occurs. Restarting the same broker, without restarting the application host, eventually settles the same process with one stock effect/activity and drained observations. This is a controlled single-node stop/start, not a network-partition, disk-loss or HA failover proof. The initial normal thirty-second test bound was rejected by evidence; this case has a two-minute completion bound and three-minute overall scenario budget, leaving normal fixture limits unchanged. These are test bounds, not an SLA.
- Orderly host stop while approved work waits for MAIN preserves its identity; restart with valid setup completes it without counting shutdown as process failure. This covers pre-dispatch pending work, not in-flight ACK/confirm drain.

Independent observation/freshness and safe per-item diagnostics remain technical extraction candidates. Settled-state meaning, MAIN selection, authorized inspection and supported recovery intent remain module policy. No new generic reliability mechanism or shared library was required.

## 5.6bc2 — Purchasing bootstrap observations

Purchasing adds four no-label instruments under `modulith_foundry.purchasing.stock_item_projection` on its existing module meter:

- `ready`: 0 or 1 for the last successfully observed bootstrap checkpoint. Before the first successful sample it is absent. Ready means the initial snapshot boundary was installed, not that the live tail is caught up, every item is correct, or replenishment is possible.
- `sample_age`: monotonic seconds since that observation, independent of outbox observations. A source failure retains readiness and increases sample age rather than substituting zero.
- `observation_failures`: failed checkpoint observations excluding requested shutdown; not failed reference-event processing.
- `bootstrap_failures`: failed attempts by the hosted bootstrap worker excluding requested shutdown. Repeated retries count again; this is a process-local counter, not a durable history and does not include explicit administrative Contract calls.

The module-owned read-only monitor uses a fresh scope, a five-second query timeout and a five-second pause. It reads the singleton bootstrap checkpoint, without locks or cross-schema reads. Missing or duplicate checkpoints are observation failures, not a fabricated unready state. Metric callbacks perform no IO. The separate worker prevents checkpoint observation faults from halting outbox observation or stopping the application. Pool/database capacity remains shared. Bootstrap itself still uses the existing fresh-scope retry and atomic snapshot installation; no recovery or consistency protocol changes.

The bootstrap worker and monitor log exception type only, without exception messages, stacks or payloads. This covers those module diagnostics, not EF/Rebus/other framework logs. Replica readiness gauges describe the same persisted checkpoint and must not be summed; each replica's sample freshness matters independently.

### Diagnose and recover

Check observation freshness before interpreting readiness. If fresh and unready with bootstrap failures, repair snapshot/subscription/storage availability and let the existing hosted retry run; do not set Ready or modify the watermark manually. If observations are stale, restore their source first. Fresh Ready alone must not trigger or suppress repair: inspect the existing Purchasing reconciliation Contract and follow the [snapshot-plus-tail recovery procedure](stock-item-bootstrap.md) when drift or failed deliveries are suspected. Preserve original delivery identities when redriving the named error queue. No automatic reconciliation, arbitrary replay, public maintenance endpoint or global source/tail lag metric is added.

### Evidence and template/library findings

`ProjectionOperationalTests` extends the existing Broker CI lane and fixture, using the agreed native metrics/logs and Inventory/Purchasing Contracts. SQL only arranges faults; it never supplies asserted projection state.

- A paused real export keeps bootstrap unready; releasing it installs the initial snapshot and exposes the reference through Purchasing's Contract.
- Removing checkpoint availability after successful bootstrap retains observed readiness and exposes staleness/failure while independent outbox observations remain fresh. Restoring the table resumes sampling without restarting the host and preserves query-visible state.
- A trigger rejects bootstrap commit. The hosted worker reports failure while the checkpoint remains unready and buffered references are not query-visible; observation remains healthy. Removing the trigger permits the existing retry to install the snapshot without restarting the host. Named module logs exclude injected private exception text.

The important distinction is observation failure versus failed bootstrap versus live-tail correctness. Readiness/freshness mechanics remain extraction candidates; checkpoint meaning, subscription/snapshot protocol and authorized repair remain consumer-owned. No generic projection framework was needed. These tests do not prove numeric OTLP export, ongoing reference-handler/reconciliation failure metrics, broker queue health, complete API replicas, full database-process outage or distributed retry limits.

## Remaining closure

The subsequent [broker/projection operations proof](broker-operations.md) supplies ongoing reference-handler/reconciliation and rebuild diagnostics, broker observations, full API replicas, representative first-insertion races, healthy-broker in-flight shutdown, controlled database interruption, cancellation-ingress death and competing publisher lease recovery. Use the [closure checklist](durability-closure-checklist.md) for current evidence and unresolved retry/stalled-backend policy limits. These historical checkpoints alone do not establish those additional guarantees or a standalone production maintenance adapter.
