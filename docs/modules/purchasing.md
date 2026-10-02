# Purchasing module charter

## Purpose

Purchasing turns an Inventory shortage recognized by the Sales-owned fulfilment process into a durable Replenishment Requirement that a purchasing user can inspect and act on later.

## Owned concepts and data

- `Replenishment Requirement`, its originating shortage operation, requested quantity, status, and stock-item reference snapshot.
- The minimal supplier and Purchase Order concepts only when a later accepted workflow exercises them.
- A Purchasing-owned Stock Item reference projection used by the late-consumer bootstrap proof.
- Purchasing-schema audit, inbox, outbox, and projection records.

The `purchasing` PostgreSQL schema is authoritative for requirements. Inventory remains authoritative for Stock Item and stock truth.

Replenishment Requirements and their compact business-operation tombstones are retained for the life of the business record in v1. Projection rows are rebuildable consumer state; inbox/outbox payloads use bounded operational retention and are not compliance audit records.

## Interface

Receiver-owned durable integration command:

- create one Replenishment Requirement for a stable shortage-operation ID.

User-facing queries:

- get an Organization-scoped requirement by its short business reference;
- list open requirements for the minimal exercise UI.

Later user-facing supplier and Purchase Order commands remain absent until the reference workflow needs them.

## Consumed and published integration contracts

Purchasing consumes the Sales-directed create-requirement command and, when the bootstrap proof is introduced, Inventory's Stock Item reference events. It publishes `ReplenishmentRequirementCreated` so the Sales Order Fulfilment Process can record the durable outcome.

The Stock Item reference projection is bootstrapped from an Inventory-owned versioned snapshot plus high watermark, then maintained from integration events. It never replays Inventory's private event-source stream.

## Invariants

- A Replenishment Requirement belongs to the same Organization as its shortage and Stock Item reference.
- Requested quantity is positive and expressed in the referenced immutable base unit.
- One shortage-operation ID creates at most one requirement; duplicate delivery returns the existing outcome.
- Reusing an operation ID for different shortage data is rejected and audited.
- Requirement creation, inbox receipt, audit, and the outgoing created event commit together.

## Authorization

Purchasing exposes the stable `purchasing-agent` role identifier from Purchasing.Contracts. It enforces `purchasing.requirements.view` and, only when those behaviors exist, module-owned supplier and purchase-order permissions. The workflow may create a requirement through its trusted capability; a browser cannot forge workflow identity.

## Replenishment receiver

The [replenishment protocol](../plans/replenishment-reliability.md) records receiver-owned durable waiting, rejection, transaction and publication behavior. Requirements retain the accepted Stock Item snapshot and use globally allocated short numbers scoped by Organization in queries. Trusted request-status queries remain distinct from authorized human requirement get/list routes. The actual Sales shortage producer/outcome handler follows in 5.4b.

## Explicit exclusions

- A complete procurement lifecycle, supplier onboarding, approvals, receiving, invoice matching, or automatic purchase-order creation.
- Reading Inventory or Sales persistence.
- Treating the local Stock Item reference projection as stock truth.
- Generic consumer-bootstrap or projection frameworks before another real consumer repeats the need.

## Stock Item reference consumer

Purchasing owns the isolated `modulith-foundry.purchasing` endpoint/error queue, per-delivery reference inbox, per-item full-state projection and bootstrap checkpoint. A native hosted worker waits for durable subscription binding before calling Inventory's trusted snapshot export. Both the worker and the administrative bootstrap Contract use the [same concrete protocol](../plans/stock-item-bootstrap.md).

Before Ready, reference rows are a durable coalesced buffer and are not query-visible. Snapshot import preserves newer per-item revisions and commits with the watermark/Ready checkpoint in one Purchasing transaction. Incoming delivery receipts and projection effects also commit atomically. The snapshot watermark never advances to the highest observed event; each item's source revision handles tail reordering. Bootstrap/reconciliation creates no replenishment or business audit replay.

`IStockItemProjectionBootstrapper` and `IStockItemProjectionQueries` are trusted workflow/administrative Contracts, not human authorization or HTTP APIs. The query supplies an explicit Organization identity and returns a Purchasing-owned view, not Inventory persistence or DTO graphs. Default EF Organization filtering still fails closed without scope. Bootstrap and validated message delivery deliberately query their own schema using explicit administrative access.

Ordinary restart retains the initialized projection/checkpoint and rebinds the same queue. `IStockItemProjectionReconciliation` supplies trusted explicit comparison/repair against a fresh Inventory snapshot, preserving newer tail rows and committing covered repairs with the boundary. It is not HTTP-exposed, an automatic repair worker or a source-restore protocol. The [protocol/runbook](../plans/stock-item-bootstrap.md) records comparison scope, repair blocking, forced-crash proofs, poison/redrive and test-only retained-queue cutover. Full-state coalescing assumes no physical Stock Item deletion; adding deletion, deltas or larger catalogs requires a reviewed protocol rather than silently widening this one.
