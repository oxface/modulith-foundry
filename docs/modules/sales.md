# Sales module charter

## Purpose

Sales captures customer demand, decides when a Sales Order is valid and approved, and owns the durable Order Fulfilment Process until the order is reserved, waiting on replenishment, cancelled, or complete.

## Owned concepts and data

- `Customer`.
- `Sales Order`, its lines, status, immutable submitted item snapshots, monetary totals, submitter, and approver.
- `Sales Approval Authority` and Sales-owned approval decisions.
- `Order Fulfilment Process`, per-line outcomes, compensation state, deadlines, and curated order activity.
- Sales-schema audit, inbox, outbox, and process records.

The `sales` PostgreSQL schema is authoritative. Sales stores the Stock Item identity and immutable SKU/description/base-unit snapshot needed to understand an order line; it never reads Inventory tables.

Sales Orders, their fulfilment outcome, and compact completed-process/business-operation tombstones are retained for the life of the business record in v1. Inbox/outbox operational payloads follow the bounded windows in the architecture plan; compliance retention and subject-data handling remain explicit later decisions.

## Interface

User-facing commands:

- create and maintain the minimum Customer record used by the reference workflow;
- create a draft Sales Order with one or more lines;
- submit a valid draft for approval;
- set a Membership's Sales Approval Authority as an authorized Sales Manager;
- approve an awaiting order within the actor's Sales Approval Authority;
- cancel an eligible order with a reason.

Queries:

- get a Customer by its Organization-scoped stable code;
- get an Organization-scoped Sales Order by its short business order number;
- list the limited order set needed by the exercise UI;
- get fulfilment status and a curated activity timeline.

The order-creation use case performs one batched Inventory contract query to validate Stock Item identities and capture their display/base-unit snapshot. It does not retain an `IQueryable`, Inventory entity, or DbContext.

## Durable collaboration

Sales owns orchestration. Its outbox sends Inventory-owned `ReserveStock` and `ReleaseReservation` integration commands and, after a shortage, a Purchasing-owned `CreateReplenishmentRequirement` command. Sales consumes Inventory reservation-outcome events and Purchasing's requirement-created event through its module endpoint.

No Sales integration event is published in v1 without an actual consumer. Starting fulfilment is an explicit Sales transaction that creates process state and outgoing outbox records with the approved order; it is not a recursive domain-event cascade.

## Invariants

- A Customer has one immutable user-supplied code unique within its Organization and a required display name. The reference creation flow canonicalizes codes to uppercase ASCII (1–64 letters/digits, hyphens, underscores or periods; first character alphanumeric), trims names (1–200 characters) and rejects name control characters. Codes identify HTTP resources; customer names need not be unique.
- Customer creation and its success audit commit together. Current actor/Organization context and `sales.customers.manage` are required for creation and administrative lookup; Organization Administrator alone does not grant Sales permissions. The v1 Customer proof deliberately excludes addresses, contacts, editing and lifecycle operations.
- A draft belongs to one Organization and Customer and contains at least one line before submission.
- Each line has a positive quantity and money expressed in the order's single currency.
- Submitted line snapshots do not change when an Inventory description changes later.
- Only an awaiting order may be approved; the submitter cannot approve the same order.
- The approver must hold the permission and satisfy Sales-owned amount/currency authority at commit time.
- Fulfilment is all-or-nothing per order line but may be partially successful across lines.
- Each line reaches one current reservation outcome for a process attempt; duplicate messages do not duplicate effects.
- Cancellation is idempotent, records a reason, and durably requests release of every successful reservation before the process becomes fully compensated.

## Authorization

Sales exposes the stable `sales-clerk`, `sales-manager`, and `sales-approver` role identifiers from Sales.Contracts. It defines and enforces permissions such as `sales.customers.manage`, `sales.orders.create`, `sales.orders.submit`, `sales.approval-authorities.manage`, `sales.orders.approve`, `sales.orders.cancel`, and `sales.orders.view`. HTTP policy checks are coarse adapters; Sales handlers re-check organization, resource state, permission, separation of duties, and approval authority for every entry path.

## Explicit exclusions

- Pricing engines, tax, discounts, invoicing, payment, shipping, returns, and complete order-to-cash behavior.
- Reading or writing Inventory/Purchasing persistence.
- A generic workflow DSL, generic saga base, mediator, or API-owned orchestration.
- Atomic reservation of all order lines or a shared multi-stream transaction abstraction.
