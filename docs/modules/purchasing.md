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

## Explicit exclusions

- A complete procurement lifecycle, supplier onboarding, approvals, receiving, invoice matching, or automatic purchase-order creation.
- Reading Inventory or Sales persistence.
- Treating the local Stock Item reference projection as stock truth.
- Generic consumer-bootstrap or projection frameworks before another real consumer repeats the need.
