# Wholesale persistence demonstration

A finite consumer of [EF ownership utilities](../../../src/ModulithFoundry.Persistence.EntityFrameworkCore/README.md)
and [Tenancy](../../../src/ModulithFoundry.Tenancy/README.md), without actor identity or Access.
It uses native Npgsql/EF registration, model configuration and save overrides. No HTTP
endpoints or authentication substitute are introduced. Inventory and Sales own their native
DbContexts, schemas, migration artifacts and history tables in the same database.

## Run

Supply `WHOLESALE_DEMO_CONNECTION_STRING` through your shell/environment for an empty,
disposable PostgreSQL database, then run from the repository root:

```bash
dotnet run --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj
```

The consumer explicitly calls Inventory's native `MigrateAsync`, then Sales's, before
initializing operation tenancy and reading/seeding fixture data. Each migration commits
independently; there is no shared transaction. Use a fresh disposable database: databases
created by E2.1's former `EnsureCreated` fixture must be recreated. Do not print or commit
connection credentials.

Expected output on first and repeat invocation:

```text
wholesale-alpha: WIDGET availability=42
wholesale-alpha: BUYER customer=Alpha Retail
wholesale-beta: WIDGET availability=7
wholesale-beta: BUYER customer=Beta Retail
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

Sales owns the `sales` schema and `CustomerReference` table. Customer codes are unique within
an Organization; the same `BUYER` code can identify different reference customers in Alpha
and Beta. Names are fixture data, not an application-user, membership or complete customer
aggregate model. Both modules set row ownership explicitly before saving.

- **Stock reference**: consumer-owned fixture availability for a SKU in an Organization.
- **Reference category**: global fixture classification, independent of an Organization.
- **Customer reference**: Sales-owned customer code/display name in an Organization.
- **Organization** and **Tenant** follow [the project glossary](../../../CONTEXT.md).

## Template recipe and proofs

[DemoComposition](DemoComposition.cs) visibly registers tenancy aliases and a native
DbContexts. [InventoryDbContext](Inventory/InventoryDbContext.cs) maps schema, entities,
keys/indexes, soft deletion, ownership and both save overrides. [Program](Program.cs)
selects the tenant before scoped reads and deliberately populates new rows before saving.
This is the exercised setup to copy/edit; no library registration facade or template generator.

[SalesDbContext](Sales/SalesDbContext.cs) reuses ownership registration/validation with its
own customer mapping. Module-local [Inventory options](Inventory/InventoryDatabase.cs) and
[Sales options](Sales/SalesDatabase.cs) configure Npgsql and a separate `__EFMigrationsHistory`
table within each schema. Runtime DI and native design-time factories call the same options
method. Schemas express module ownership; they do not restrict the database credentials.

## Native migrations

Restore the pinned local `dotnet-ef` tool from the repository root:

```bash
dotnet tool restore
dotnet ef migrations list --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj --context InventoryDbContext --no-connect
dotnet ef migrations list --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj --context SalesDbContext --no-connect
```

For a later model change, choose a descriptive new migration name and scaffold only its
owning context. For example:

```bash
dotnet ef migrations add InventoryChange --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj --context InventoryDbContext --output-dir Inventory/Migrations
dotnet ef migrations add SalesChange --project samples/Wholesale/PersistenceDemo/PersistenceDemo.csproj --context SalesDbContext --output-dir Sales/Migrations
```

Factories build a model with uninitialized tenancy, without executing the console or opening
a database. If no environment connection is supplied, they use a non-operational placeholder
for scaffolding. Actual database commands require the disposable environment connection.
Snapshots are kept beside their module migrations; review the tool's output paths as well
as its generated code.
Review generated C#, format it and apply repository semantic conventions before building;
generated code remains editable consumer code. Native context attributes select the correct
migrations/snapshot within the shared sample assembly. No separate migration projects or
runtime code generation are needed here.

Initial migrations create only the owning module's tables and indexes. The schema policy
tests intentionally require review before admitting new operation kinds, foreign keys or
raw SQL. They do not parse SQL or promise a universal migration-isolation checker.

## Proofs and remaining work

[Sample proofs](../PersistenceDemo.Tests/OwnershipTests.cs) run the real composition against
PostgreSQL. They cover cached-model isolation, tenantless/unestablished failures, global saves,
validation through all save overloads, foreign/missing/changed ownership, detached foreign IDs,
model classification and first/repeat console invocation. The separate GUID-key consumer in
`tests/PersistenceTests` runs without either context library.

[Module migration proofs](../PersistenceDemo.Tests/ModuleMigrationTests.cs) initialize either
module first, verify its tables/history alone, preserve its rows when the other module is
initialized, verify independent histories and exercise Sales tenant filtering/save guards.
[Container-free model/artifact policies](../../../tests/ArchitectureTests/ModulePersistenceTests.cs)
check both schemas, explicit entity classification and snapshot/model consistency.

Relationships, separate version conflicts and shared
cross-module transactions remain subsequent increments, as recorded in
[the E2 plan](../../../docs/plans/e2-persistence.md). E2.2 proves a consumer-owned migration
recipe; it introduces no new reusable library mechanism.
