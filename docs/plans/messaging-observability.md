# Messaging operational signals

Increment 5.6b2a adds module-owned outbox observations and delivery-failure/receipt-suppression counters. It does not add automatic replay, a generic operations service, public diagnostic routes or a new telemetry dependency.

## Collection and ownership

Inventory, Sales and Purchasing each expose a native .NET meter named `ModulithFoundry.{Module}.Messaging`. The API subscribes these meters through its existing OpenTelemetry configuration. Generic Host owns `IMeterFactory`; each module's singleton instruments are shared with its isolated Rebus services rather than constructed twice. No Aspire runtime dependency is introduced.

Each module samples only its own unpublished outbox rows through a separate, read-only hosted worker. Sampling covers all Organizations deliberately: this is privileged process diagnostics, not a tenant query or a product API. It uses PostgreSQL wall-clock time, a five-second command timeout and a five-second pause after each observation. A slow/failed query cannot block a metric collection callback or prevent the independent relay loop from running, although database/pool capacity is still shared.

Observable callbacks read an immutable in-memory sample only. Before the first successful observation, backlog/age/lease gauges are absent, not zero. After a sampling failure the previous sample remains, its freshness age increases and a failure counter/log is emitted. Do not interpret an old zero as evidence that work has drained. Host shutdown cancels sampling normally without recording a failure.

This follows the native [.NET metric guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation): use host-owned `IMeterFactory` for lifetime/isolation and keep observable callbacks fast. This implementation uses existing platform and repository dependencies, not a new metric library.

## Published instruments

Names have the prefix `modulith_foundry.{inventory|sales|purchasing}`. There are no metric labels containing Organization, user, message, process, payload, error text or dynamically supplied message type.

| Suffix | Type/unit | Meaning |
| --- | --- | --- |
| `outbox.pending` | Gauge/messages | All committed unpublished rows, including leased or retry-delayed rows; not just immediately eligible work. |
| `outbox.oldest_age` | Gauge/seconds | Oldest unpublished creation age at the last successful observation; zero for a successfully observed empty backlog. |
| `outbox.expired_leases` | Gauge/messages | Unpublished rows whose lease deadline has passed, including rows with a future retry schedule. Not a count of successful lease reclamations or evidence that a publisher is dead. |
| `outbox.sample_age` | Gauge/seconds | Monotonic elapsed time since the last successful observation. Interpret backlog/lease/age gauges with this freshness signal. |
| `outbox.observation_failures` | Counter/failures | Failed database observations, excluding requested shutdown. |
| `outbox.relay_failures` | Counter/failures | Failed claim/relay-loop iterations, including a failure while rescheduling an attempted dispatch. |
| `outbox.dispatch_failures` | Counter/failures | Failed serialization, publication or dispatch marking. Broker publication may already have succeeded. |
| `inbox.duplicates` | Counter/deliveries | Validated delivery identities suppressed by a retained, matching inbox receipt. A new delivery identity for an existing semantic operation is not included. |

Counters are process-lifetime observations, not durable audit totals or exactly-once counts. Receipt suppression can be observed again after redelivery. Conflicting fingerprints are not successful duplicates. The receipt counter currently covers Inventory reserve/release, Sales reservation/release/replenishment outcomes and Purchasing replenishment commands; the separate Stock Item projection inbox is not silently included.

Replicas observe the same database backlog: **do not sum backlog/age/lease gauges across replicas**. Retain service-instance resource identity and use fresh per-instance observations (for example, a maximum of fresh backlog samples). Counter rates may be aggregated across instances when their interpretation warrants it. Configure exporter/backend access as privileged operational access; no unauthenticated metrics endpoint is added.

## Diagnose before recovery

1. Check sample freshness and observation failures first. A missing/stale sample calls for database/worker diagnosis, not replay.
2. Rising pending depth/oldest age with relay failures points toward claim/storage availability; dispatch failures point toward serialization, broker publication or dispatch marking. These signals overlap and must not be summed into a fabricated business-failure total.
3. Expired leases with pending work justify checking publisher health and retry scheduling. An expired lease can still belong to a slow live publisher. The existing relay may reclaim eligible work; do not clear live leases or invent a new operation identity.
4. Module relay/monitor logs carry fixed module context, message identity/attempt where known and exception type only. They deliberately omit exception messages, retained payloads, credentials and tokens. Use controlled broker/error-queue evidence to correlate the message; do not paste payloads into telemetry or support tickets. This is not a claim that every third-party/application log has been audited by this increment.
5. Zero unpublished rows proves only that publication marks were observed. It does not prove destination receipt or process completion. Inspect owning-module process Contracts and the named error queue before applying the [retained-outcome recovery procedure](message-delivery-recovery.md).
6. Choose destination redrive or explicitly supported source republication only after repair, preserve original transport/business identities, and verify the receiver's business state. Do not delete inbox receipts or semantic tombstones to make a retry pass. Sales/Purchasing do not gain arbitrary publisher-requeue Contracts here.

## Evidence and template/library findings

The Broker CI lane exercises the native `MeterListener`/module log boundary alongside real PostgreSQL/RabbitMQ and existing Contracts. Tests create committed backlog through real workflows, block relay claims, then prove observations clear after publication. They also cover unavailable observation data retaining backlog/freshness, a publish-success/dispatch-mark-failure ambiguity with private exception text, expired retained leases and recovery, and matching receipt suppression in all three endpoints without another stock/process/requirement effect. SQL only arranges faults, backlog schedules and leases; asserted state comes through Contracts, actual broker outcomes and the agreed telemetry surface.

The separation of database observation from collection callbacks, sample freshness, bounded label cardinality and safe relay diagnostic fields are extraction candidates. Persistence queries remain module-owned; interpretation of receiving workflow state, supported recovery intent and authorization remain business/module choices. No shared instrumentation or recovery framework is extracted now.

### OTLP export boundary proof

The existing `LocalRuntime_RepeatedLifecycle_ProvidesHealthyApiAfterMigrations` Topology test also requires an HTTP/protobuf metric export containing all three module meter names and their `outbox.pending` instrument names. Each of its two complete application lifecycles uses a fresh controlled receiver, so a previous API process's exports cannot satisfy a later assertion. The receiver accepts `/v1/metrics` alongside the existing log/trace routes; assertions inspect exported name strings, not internal metric classes or numeric protobuf fields.

Only this test sets `OTEL_METRIC_EXPORT_INTERVAL=1000`, using the standard [OpenTelemetry periodic-export configuration](https://opentelemetry.io/docs/specs/otel/configuration/sdk-environment-variables/). Production intervals, API composition and dependencies are unchanged. Metric arrival has a separate twenty-second bound after readiness/log export within the existing startup deadline.

This proves that module observation, host meter subscription and the configured API exporter are connected across a real application-process boundary. It does not prove a production collector's retention, dashboard queries, alerts or exported numeric semantics; native Broker tests remain the source of evidence for backlog values, freshness and counters. The proof failed without the receiver route, passed with it, and failed again when the Sales meter subscription was temporarily omitted; that subscription was restored afterward. The controlled OTLP receiver is test infrastructure, not a production library extraction candidate.

## Remaining proof and operational work

- Native instrument behavior and the API's configured OTLP export boundary are covered separately. Production collector/backend retention, queries and alerts require deployment-specific evidence.
- [Workflow operational observations](workflow-operations.md) add independent Sales process count/creation age/freshness and dispatch/observation failures, plus Purchasing bootstrap readiness/freshness and bootstrap/observation failures. Ongoing reference-handler/reconciliation and Inventory rebuild failures, broker-owned ready/unacknowledged/error-queue depth, broadly usable maintenance adapters and deployment alert thresholds remain subsequent 5.6b work.
- These observations do not close 5.6c's full-application replica/isolation, graceful shutdown, infrastructure interruption, cancellation-ingress death or globally bounded poison-retry matrix.
- Counts scan unpublished rows only, using the existing partial pending index as applicable. Tune polling/indexes from measured backlog and replica/database load rather than adding options or extra indexes speculatively. Sampling failures preserve diagnostics, not database availability guarantees.
