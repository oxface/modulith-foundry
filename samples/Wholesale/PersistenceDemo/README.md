# Wholesale persistence demonstration

A finite consumer of [EF ownership utilities](../../../src/ModulithFoundry.Persistence.EntityFrameworkCore/README.md)
and [Tenancy](../../../src/ModulithFoundry.Tenancy/README.md), without actor identity or Access.
It uses native Npgsql/EF registration, model configuration and save overrides. No HTTP
endpoints, authentication substitute or migrations are introduced in this first increment.

## Run

Supply `WHOLESALE_DEMO_CONNECTION_STRING` through your shell/environment for an empty,
disposable PostgreSQL database, then run from the repository root:

```bash
dotnet run --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj
```

The consumer explicitly calls native `EnsureCreatedAsync` for this fixture. It is not a
migration runner and should not be pointed at a database using another initialization
strategy. The following E2 increment owns reviewed module migrations. Do not print or commit
connection credentials.

Expected output on first and repeat invocation:

```text
wholesale-alpha: WIDGET availability=42
wholesale-beta: WIDGET availability=7
```

## Ownership and language

Inventory owns the `inventory` schema, `StockReference` and `ReferenceCategory` tables and
reference-data reads. These are persistence fixtures, not rich stock aggregates or reservation
logic. Each Organization has its own SKU/quantity rows; Organization keys map explicitly from
the selected technical tenant. Initial quantities 42/7 are consumer-supplied. Soft deletion
has its own named filter; it can be disabled while Organization filtering remains active.

Reference categories are explicitly global sample data. Their saves/queries need no tenant;
that fixture policy does not grant access to Organization-owned rows. The sample has no
membership or permission mechanism; those enter in E3.

- **Stock reference**: consumer-owned fixture availability for a SKU in an Organization.
- **Reference category**: global fixture classification, independent of an Organization.
- **Organization** and **Tenant** follow [the project glossary](../../../CONTEXT.md).

## Template recipe and proofs

[DemoComposition](DemoComposition.cs) visibly registers tenancy aliases and a native
DbContext. [InventoryDbContext](Inventory/InventoryDbContext.cs) maps schema, entities,
keys/indexes, soft deletion, ownership and both save overrides. [Program](Program.cs)
selects the tenant before scoped reads and deliberately populates new rows before saving.
This is the exercised setup to copy/edit; no library registration facade or template generator.

[Sample proofs](../PersistenceDemo.Tests/OwnershipTests.cs) run the real composition against
PostgreSQL. They cover cached-model isolation, tenantless/unestablished failures, global saves,
validation through all save overloads, foreign/missing/changed ownership, detached foreign IDs,
model classification and first/repeat console invocation. The separate GUID-key consumer in
`tests/PersistenceTests` runs without either context library.

Two-module persistence, relationships, migrations, separate version conflicts and shared
cross-module transactions remain subsequent increments, as recorded in
[the E2 plan](../../../docs/plans/e2-persistence.md).
