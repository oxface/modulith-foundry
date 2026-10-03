# Durability closure before Increment 8.1

This checklist tracks the accepted 5.6b/c work. Evidence closes a specific window, not a universal exactly-once guarantee. The owner accepted the local-phase limits below on 2026-10-02 and authorized Increment 8.1. New change sets still require owner review and explicit commit approval; 6 and 7 are not pulled forward.

## Operational coverage

- [x] Module outbox depth/age/lease/freshness and receipt/failure instruments; native numeric and OTLP wiring proofs.
- [x] Sales process observations, per-process isolation and safe diagnostics.
- [x] Purchasing bootstrap readiness/freshness, failed bootstrap retry and independent observation recovery.
- [x] Ongoing Purchasing reference-processing/reconciliation and Inventory rebuild failure diagnostics.
- [x] Native broker ready/unacknowledged/error-queue inspection and identity-preserving recovery guidance.
- [x] Existing trusted recovery Contracts and platform tools suffice without another maintenance adapter for this local phase.

## Failure coverage

- [x] Competing endpoint receiver replicas and post-commit/pre-ACK death, separately from full APIs.
- [x] Controlled broker stop/start with committed intent and recovery without host restart.
- [x] Orderly shutdown before dispatch and retained-work restart.
- [x] Two complete API replicas, shared hosted workers and the authenticated workflow.
- [x] First-time competing receipt/semantic-operation insertion on representative Inventory, Sales and Purchasing operations.
- [x] In-flight graceful shutdown and its tested drain/redelivery bounds (healthy broker, receiver exits within 20 seconds).
- [x] Database stop/start recovery with retained infrastructure.
- [x] Cancellation-ingress death around commit and deliberately discarded response.
- [x] Abandoned publisher lease recovery across replicas (production 30-second lease, already-running survivor).
- [x] Cross-replica ordinary poison forwarding/redrive with two fixed live receivers, distinguishing per-process attempts from global limits. Global-cap policy remains below.

## Closure rules

Tests use the already approved Contracts, authenticated HTTP, native metrics/logs and real RabbitMQ/error queues. SQL, container lifecycle and process barriers arrange faults only. Report actual tested timing bounds, rejected assumptions, reusable mechanics versus module policy and every remaining limit. Do not silently accept an unproven gap or manufacture a business failure from a missing response. A required new architectural choice or explicit acceptance of a limitation must return to the owner.

## Review and policy gate

See [broker operations and evidence](broker-operations.md). Complete-API ingress proofs use a real PostgreSQL trigger barrier before commit and a transactional NOTIFY after commit; SQL observes coordination only. Both owned API processes are terminated, then restarted against retained infrastructure. The post-commit test deliberately discards any HTTP response; it does not prove termination before the server emitted response bytes. Same-VM cookies/tickets survive, but multi-node key-ring deployment remains a separate requirement.

First-time Inventory, Sales and Purchasing representative races are proven for identical MessageId and new MessageId/same semantic operation. Purchasing's existing checkpoint row lock deliberately serializes creation rather than allowing two uncommitted creations to reach insertion together.

Owner reconciliation on 2026-10-02 accepted these limits for local-phase closure:

- Retain Rebus's process-local three-attempt tracker with platform alerts/manual quarantine; do not introduce a global retry counter or queue migration now. A restart/another replica can reset the local count; bounded ordinary poison forwarding is not a global guarantee. Revisit before multi-replica public production if the measured load/cost warrants stronger policy.
- Healthy-broker in-flight receiver shutdown is proven. The pinned transport's explicit `SetPublisherConfirms(true)` selects a 60-second value, but the transport only stores it: it does not apply that value or an application cancellation token to `BasicPublishAsync`. Changing the option alone does not establish a publish-latency bound. Stalled confirmation/telemetry-exporter shutdown remains unproven. This is an accepted local-phase gap, not deployment acceptance: bounded publication and shutdown under unavailable confirmation/telemetry backends require a separate proof before the Slice 7 deployment gate.

No additional generic maintenance adapter is justified by the tested scenarios: native broker observations and trusted module recovery/reconciliation Contracts provide the seams. The owner accepted this scope; it does not claim a standalone production operator application exists.
