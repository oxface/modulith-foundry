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

Inventory exposes the stable `inventory-manager` role identifier from Inventory.Contracts. It enforces `inventory.items.manage`, `inventory.locations.manage`, `inventory.stock.adjust`, `inventory.stock.view`, and trusted fulfilment capabilities. Browser actors cannot invoke workflow-only reserve/release operations merely by knowing their message shape.

## Explicit exclusions

- General event-sourcing framework, separate hydration-checkpoint snapshots, generic upcasters, async projection framework, or multi-stream transaction abstraction.
- Warehouse zones/bins, picking, transfers, costing, lot/serial tracking, and units-of-measure conversion.
- Exposing private stream JSON as the product activity timeline or consumer-replay interface.
- Reading Sales or Purchasing tables.
