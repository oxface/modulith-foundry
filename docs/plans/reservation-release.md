# Reservation Release protocol

## Responsibility and boundary

Inventory owns `ReleaseReservationV1` and `StockReservationReleaseOutcomeV1`. The command is directed to the existing Inventory queue; the outcome is published on its own stable logical topic. There is no HTTP release route. The private event `inventory.stock-position.reservation-released`, version 1, is not an integration event.

Release identifies the Organization, fulfilment process, order/line, original reservation operation and reservation, target item/location, and a distinct compensation-operation ID. Quantity and unit come from Inventory's retained original reservation outcome and matching child state, never from the caller. Deactivated references still allow cleanup.

## Decisions and identities

- `Released`: append one release fact, retain the ended child, reduce reserved quantity, preserve on-hand quantity, and publish the original quantity/unit.
- `AlreadyReleased`: a new compensation operation targets an already ended reservation; record its result without a second stock event.
- `Rejected`: no matching successful original operation, mismatched correlation, or conflicting reuse of a compensation-operation ID. Rejection omits quantity/unit. Unknown reservation is not successful compensation: Sales must wait for the original result before issuing release.
- Unreadable original outcome, missing required write model or inconsistent reservation state is a technical integrity failure, not a successful/rejected business decision. Repair and explicitly redrive the same message.

The incoming MessageId/content fingerprint is separate from the compensation-operation fingerprint. An identical committed delivery does nothing; altered content under that MessageId is poison. Equivalent intent with a new MessageId adds only an inbox receipt. Conflicting operation reuse publishes a rejection without replacing the first operation receipt. Retain both reserve and release semantic receipts while messages can replay; a delayed reserve replay must not resurrect stock.

The Rebus adapter checks producer, transport MessageId and process correlation before invoking the Rebus-free handler. Broker access is the trust boundary; producer headers are not cryptographic authentication. No new transport abstraction is introduced.

## Atomicity, replay and repair

One explicit Inventory transaction owns the release-operation receipt, inbox, event append, required inline projection, audit and outgoing outcome. Existing Organization writer coordination and stream optimistic concurrency arbitrate competing operations. The relay preserves outgoing identity across ambiguous publication. Broker delivery remains at least once.

Historical evolution applies the recorded release; it does not revalidate today's reference eligibility or republish integration effects. The optional child `IsReleased` flag defaults to false for pre-release JSONB write models. Rebuild reconstructs active and ended reservation identities from private events. Before deciding release, the handler checks that active child quantities total the stored reserved quantity, preventing a stray released flag from falsely reporting completed compensation.

## Evidence and limits

`InventoryReservationReleaseTests` exercises the existing broker/Contracts seams against PostgreSQL and RabbitMQ. Cases cover release quantity/history, original unit, deactivated references and legacy JSON shape, duplicate delivery/lost ACK/new operation, delayed reserve replay, conflicting identities and foreign correlation, competing releases, every atomic participant's rollback and repaired redrive, damaged projection repair, and receiver death around commit/publication. Process/SQL barriers arrange faults only; Contracts and outcome subscribers assert product behavior.

Remaining limits:

- The original reserve receipt lacks target stream/item/location identity. A wrong target and a lost lookup projection cannot always be distinguished: both fail technically rather than falsely completing compensation. Revisit identity preservation at the event-sourcing correctness gate.
- Required projection lookup still locates temporal reads. After its deletion, repair needs a known stream ID; business-key temporal lookup is unavailable until repair.
- Ended children remain in the required projection, so its reservation bag grows. Measure realistic streams and retain identity safely before designing compaction/retention.
- Targeted consistency checks are not proof against arbitrary self-consistent projection tampering. Independent fixtures and governed rebuild remain necessary.
- These are receiver-process crash proofs, not broker/database crash or multi-replica guarantees. Sales cancellation is described below; broader operational failures remain 5.6.

The separate [5.6a replica proof](broker-durability.md) now exercises Inventory reserve/release and Sales release-outcome consumers with two simultaneous receiver processes. It closes the committed-before-ACK failover window, not every multi-replica race or infrastructure failure.

## Template/library findings

The receiver reuses existing inbox/outbox, isolated Rebus endpoint, lease and explicit transaction mechanics for a second Inventory command. No new generic framework is needed. Envelope checks, stable aliases and lease dispatch remain extraction candidates. Reservation correlation, quantity ownership, semantic receipts, outcome policy and projection definition remain Inventory-owned. The private release event demonstrates an irreversible fact with an idempotent command boundary, not an event-store-level deduplication mechanism.

## Sales cancellation and compensation

`ISalesOrderCancellation` is the human-facing capability. The organization-scoped `POST .../sales/orders/{orderNumber}/cancel` adapter requires BFF antiforgery; the handler verifies current `sales.orders.cancel`, expected order version and a trimmed 1–500-character printable reason. The sample permits cancellation of draft, awaiting and approved orders. One Sales transaction commits cancellation metadata/version, process cancellation intent/version, stable release IDs/outbox messages for known successful reservations, human activity and audit. Approved demand without its required process fails as an integrity fault rather than silently skipping compensation.

The Sales Order becomes `cancelled` immediately; this does not imply stock release has completed. The process remains `compensation-pending` while any dispatched reservation has no outcome or any successful reservation lacks a successful release outcome. A permanent release rejection yields `attention-required`. Only known no-stock-effect outcomes or successful `released`/`already-released` outcomes permit `compensated`. Reservation facts and release progress are separate fields, not overwritten line statuses.

No new release dispatcher is needed: stable release intent and outbox rows commit together. The existing relay routes release to Inventory. Cancellation before MAIN discovery closes undispatched intent; the discovery/replenishment worker excludes cancelled demand and rechecks it in the aggregate. Already committed reserve/replenishment commands are not deleted: a relay may already have published them. Late successful reservations atomically stage release with the accepted original outcome. Late shortages do not stage new replenishment; existing Purchasing requirements and arriving outcomes are retained without reopening fulfilment. `compensated` means Inventory reservation compensation, not cancellation of Purchasing's independent work.

The release outcome receiver matches tenant, process, order/line, original reservation operation/identity, compensation operation, command causation and successful quantity/unit. Unmatched outcomes are receipted/audited without progress. Delivery fingerprint and retained semantic fingerprint remain separate; duplicates add no repeated transition or completion activity, while conflicting decisions are poison. One Sales transaction commits process/owned-line changes, inbox, activity and audit. Parent process-version optimistic concurrency arbitrates release, cancellation and original outcome races; losers require a fresh scope. Human cancellation reports a version conflict for a concurrent loser, while Rebus retries receiver conflicts. An already cancelled order returns its first cancellation unchanged after current authorization, including on a lost-response retry with the original positive expected version; it never replaces reason/actor/time.

### Sales proof boundary

The existing Broker lane covers real reserved-line release and mixed shortage compensation, cancellation before discovery/approval and late outcomes, competing cancellation requests, each request/outcome atomic participant's rollback/redrive, correlation/semantic idempotency and forced Sales receiver termination before/after commit. Controlled-peer cases use real RabbitMQ and Sales persistence for protocol ordering; only real Inventory cases assert physical stock. The test-only child runs production Sales composition on the retained queue; it is not a new production service. The authenticated topology journey covers CSRF, current permission denial, conflict/reason Problem Details, textual progress, release history and retry timestamps.

Five-minute reservation/release response deadlines are durable diagnostic timestamps, not evidence that Inventory failed. Technical retry exhaustion or an ambiguous missing response must not manufacture compensation: it leaves pending work for the 5.6 reconciliation/error-queue runbook. No automatic error-queue notification, bounded cross-replica retry counter, semantic-receipt cleanup or Purchasing cancellation workflow is claimed here. Cancellation ingress itself is tested with transaction fault injection; its dedicated API-process termination/publication matrix remains in 5.6, alongside replica and infrastructure failures.

### Additional template findings

Concrete EF process state, stable operation IDs and module-owned outboxes suffice for this compensation proof; no generic saga base or transport-dependent business handler is necessary. The implementation rejects the shortcut of deleting queued intent or treating unknown reservation as successful cleanup. Release correlation and completion policy remain Sales-owned. Inbox/outbox/adapter mechanics are repeated extraction candidates, but retry exhaustion and operational reconciliation still need explicit proof before promising a reusable reliability API.
