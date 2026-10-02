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
- These are receiver-process crash proofs, not broker/database crash or multi-replica guarantees. Cross-module cancellation and late-result races belong to 5.5b; broader operational failures remain 5.6.

## Template/library findings

The receiver reuses existing inbox/outbox, isolated Rebus endpoint, lease and explicit transaction mechanics for a second Inventory command. No new generic framework is needed. Envelope checks, stable aliases and lease dispatch remain extraction candidates. Reservation correlation, quantity ownership, semantic receipts, outcome policy and projection definition remain Inventory-owned. The private release event demonstrates an irreversible fact with an idempotent command boundary, not an event-store-level deduplication mechanism.
