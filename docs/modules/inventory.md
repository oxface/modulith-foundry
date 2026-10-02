# Inventory module charter

## Purpose

Inventory decides what is stockable, where stock is held, how physical quantity changes, and whether a particular sales demand can reserve quantity at one Stock Position.

## Owned concepts and data

- State-stored `Stock Item` and `Stocking Location` records.
- One event-sourced `Stock Position` stream per Organization, Stocking Location, and Stock Item.
- `Reservation`, `Reservation Release`, and `Stock Movement` facts within that stream.
- The inline current-state Stock Position projection and Inventory audit/activity records.
- Inventory-schema inbox, outbox, event-stream, stream-metadata, and projection data.

The `inventory` PostgreSQL schema is authoritative. No other module reads its tables or private event streams.

Stock Position streams are retained as immutable business history for the life of the Stock Position in v1. Published outbox payloads and inbox receipts use bounded operational retention; audit/export/redaction requirements remain compliance-driven rather than inferred from the event store.
Ending an event-sourced aggregate is a domain lifecycle transition recorded by an appended event,
not deletion of its stream. View-specific projections may omit ended aggregates while replay still
reconstructs their terminal state; the required Stock Position write model retains identity for
business-key lookup. Any future privacy erasure or destructive sanitization is a
separate, explicitly governed retention operation rather than an ordinary domain command.

## Interface

User-facing commands and queries:

- create or update the minimal Stock Item and Stocking Location reference data;
- record an initial receipt or later physical quantity increase;
- reconcile an existing Stock Position to an observed non-negative on-hand quantity with a required reason and expected version, rather than rewriting event history;
- get current Stock Position state, state at an exact event version or inclusive recorded instant, and bounded curated business history under current view permission;
- rebuild and verify the inline current projection through an explicit administrative operation.

In-process module queries:

- resolve a batch of Organization-scoped Stock Item identities to stable reference snapshots for Sales;
- export the versioned snapshot and high-watermark data required by the accepted late-consumer bootstrap proof.

Receiver-owned durable integration commands:

- reserve one order-line quantity with a stable business-operation ID;
- release one reservation with a stable compensation-operation ID.

## Published integration events

- reservation succeeded, including the reservation and operation identities;
- reservation shortage, including requested and available quantity;
- reservation released;
- Stock Item reference changed, introduced only with the real Purchasing bootstrap consumer.

These are explicit versioned integration contracts, not the Stock Position's private event-source event types.

## Invariants

- A Stock Position identity is unique for `(Organization, Stocking Location, Stock Item)`.
- Quantity uses the Stock Item's immutable v1 base unit and constrained decimal representation.
- On-hand and reserved quantity cannot produce negative available stock.
- One reservation operation either reserves the full order-line quantity or records shortage; it does not partially reserve a line.
- Repeating the same business-operation ID returns the same semantic outcome without appending a second effect.
- Releasing an already released reservation is idempotent; conflicting reuse of an operation ID is rejected and audited.
- Expected stream version controls optimistic append; stream events and the inline projection commit atomically.
- Hydration and new decisions use the same deterministic evolution function; replay never republishes historical effects.

## Authorization

Inventory exposes the stable `inventory-manager` role identifier from Inventory.Contracts. It enforces `inventory.items.manage`, `inventory.locations.manage`, `inventory.stock.adjust`, `inventory.stock.view`, `inventory.projections.rebuild`, and trusted fulfilment capabilities. Rebuild is an explicitly authorized administrative in-process capability, not a mapped HTTP endpoint. Browser actors cannot invoke workflow-only reserve/release operations merely by knowing their message shape.

## Explicit exclusions

- General event-sourcing framework, separate hydration-checkpoint snapshots, generic upcasters, async projection framework, or multi-stream transaction abstraction.
- Warehouse zones/bins, picking, transfers, costing, lot/serial tracking, and units-of-measure conversion.
- Exposing private stream JSON as the product activity timeline or consumer-replay interface.
- Reading Sales or Purchasing tables.

## Durable reservation receiver

The concrete `Messaging/ReserveStockMessageHandler` is the Rebus adapter: it checks producer/transport identity and passes the host shutdown token to the application handler. `Reservations/ReserveStockHandler` owns validation, business decision and atomic persistence without reading ambient broker headers or depending on Rebus interfaces. This separation is concrete for the current command; shared envelope/pipeline mechanics remain a later extraction, not a generic messaging framework here. Register the endpoint once per host; separate module providers are supported, duplicate registration is not an idempotent no-op.

`ReserveStockV1` is directed workflow intent, not an HTTP or browser-authorized command. `StockReservationOutcomeV1` is an explicit integration event with a stable logical alias/topic, independent of CLR namespaces. Its `AvailableQuantity` is the quantity after a successful reservation, or the observed quantity when recording shortage. Outcomes use textual wire enum values. The private `StockReserved` event remains a distinct event-source contract.

Inventory owns the `modulith-foundry.inventory` input queue, `modulith-foundry.inventory.error` error queue, isolated Rebus provider, handler and relay. The host starts them in the existing application process. Fresh message scopes start without a human Organization context; validated workflow metadata supplies an explicit Organization only for that scope. Own-schema query filters then protect Stock Item, Stocking Location and Stock Position lookup. Workflow audits/event metadata identify `sales.order-fulfilment`, not a fabricated human User. The private broker's credentials/queue access are the trust boundary: `producer-module=sales` is not cryptographic authentication.

One explicit Inventory transaction commits delivery inbox receipt, semantic-operation tombstone, event append, reservation-bearing inline write model, audit and outgoing outcome. Stream-version concurrency arbitrates competing reservations; a technical conflict rolls back and Rebus retries in a fresh scope. Rebuild uses the existing write gate and reconstructs reservation identities from events without replaying messages or audits.

Delivery identity and business identity are separate. An identical MessageId/content does nothing after commit; altered content under that MessageId is poison. A new MessageId carrying the same OperationId/business content records only a receipt: it does not reserve or publish again. Quantity scale is canonicalized for the semantic fingerprint. A conflicting reuse produces a distinct rejection/audit without replacing the first operation's stored outcome; future Sales handlers must correlate causation as well as OperationId. The v1 fingerprint's explicit field selection is a compatibility contract, not reflection over an evolving DTO.

The module-local relay atomically leases committed rows with `FOR UPDATE SKIP LOCKED`, a token, and PostgreSQL time. It polls every two seconds when idle, publishes with RabbitMQ publisher confirms, and marks dispatched only for its token. Failed/ambiguous sends or dispatch marks retry with the same outgoing MessageId and payload, with backoff capped at 64 seconds. A 30-second abandoned lease becomes claimable. Publication can duplicate; this is at-least-once delivery, not exactly-once transport. A publish confirmation does not prove that a subscriber exists, so consumers must establish durable subscriptions before producers are enabled. The first receiver has no input subscriptions because it currently accepts only directed commands.

### Proof limits and follow-ups

- Broker tests use real transports with a subscriber bound before receiver startup. In addition to injected rollback and ambiguous dispatch marking, a test-only executable starts the actual Inventory receiver in a separate OS process. Tests kill that process before commit, after commit/before ACK, and after publication/before dispatch marking, then restart against the same PostgreSQL/RabbitMQ. They prove rollback/redelivery, one stock effect and stable outgoing identity after the real abandoned lease expires. They do **not** prove broker/database process death or multiple application replicas; the broader 5.6 matrix retains those cases.
- Rebus technical retry tracking is process-local; three configured delivery attempts are not a durable cross-replica limit. Module error-queue forwarding is distinct from RabbitMQ TTL/dead-letter policy. No distributed retry counter is added here.
- Publishing has no application cancellation/verified confirmation-timeout bound in this transport version. Broker stalls and shutdown timing require the later failure/operations proof. An expired lease may allow duplicate publication during a slow send; stable identity is intentional protection for consumers.
- There is no inbox/outbox cleanup worker yet. Retain semantic-operation tombstones while replays/retries remain possible; inbox expiry must never permit a second reservation. Scheduled retention, poison inspection/redrive and alerting are operational follow-ups, not capabilities claimed by this increment.
- Sales now enqueues reservation commands and consumes outcomes through its own endpoint; its MAIN lookup uses a trusted Inventory reference contract. Sales cancellation/compensation and reliability extraction remain separate planned increments.

## Durable release receiver

The authorized [delivery-recovery Contract and runbook](../plans/message-delivery-recovery.md) inspect retained publication metadata and explicitly requeue reservation/release outcomes with their original identity. Inventory Manager gains `inventory.message-deliveries.recover`; no public route is mapped. Recovery changes only retained scheduling plus operator audit, not stock, private events or business-operation receipts. Active leases and stale attempt snapshots are refused; publisher confirmation is not receiver completion.

The [release protocol](../plans/reservation-release.md) adds a second directed command on the same isolated endpoint. Inventory validates the retained original reservation and its process/order/line correlation, owns release quantity/unit, and commits its inbox, separate compensation-operation receipt, private release event, inline state, audit and outgoing outcome atomically. Ended child identity is retained through replay/rebuild. Duplicate intent cannot release twice or let a delayed reserve replay resurrect stock. Inconsistent required state fails technically and requires repair/redrive; it is not reported as completed compensation. No HTTP capability is exposed. Sales cancellation now supplies durable release intent and consumes the outcome without accessing Inventory tables.

## Stock Item reference export and publication

The [bootstrap protocol](../plans/stock-item-bootstrap.md) uses a separate transactional, commit-ordered Stock Item reference feed, complete-state `StockItemReferenceChangedV1` outbox messages, and a bounded read-only Repeatable Read snapshot with its high watermark. Revision is technical persistence metadata, not Stock Position sequence order or an aggregate business rule. Every real Stock Item mutation acquires the feed lock before loading/mutating and commits reference revision, business audit and outgoing event together. No-op commands do not advance it. Existing pre-feed records have baseline revision zero and are covered by export.

The full-catalog exporter deliberately bypasses the named Organization filter, includes inactive items and carries tenant identities. It is a trusted bootstrap contract, never a browser endpoint. Stock Item metadata writes serialize across Organizations; stock quantity workflows do not acquire this lock. The accepted sample export is bounded at 10,000 items. Purchasing consumes this protocol without reading Inventory tables or private events; reconciliation/cutover/failure closure remain 5.3b.
