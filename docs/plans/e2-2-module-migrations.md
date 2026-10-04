# E2.2 module-owned schemas and migrations

Status: implemented for owner review on 2026-10-04, after E2.1 checkpoint `50e7933`.
The owner approved proceeding with the proposed two-module migration slice. Generated
migrations and consumer setup remain subject to ordinary code review; no commit is authorized.
[The report](../reports/e2-2-module-migrations.md) records fresh verification and limits.

## Outcome

The finite PersistenceDemo explicitly initializes Inventory and Sales in one PostgreSQL
database using their own native DbContexts, schema mappings, migrations, model snapshots
and history tables. The current ownership utility is reused without changing its interface.
This is a sample/template recipe, not a new library migration runner or registration facade.

Inventory continues owning stock reference rows and global reference categories. Sales owns
Organization-specific customer reference rows with an ID, consumer-populated Organization
key, customer code and display name. These are persistence fixtures, not complete aggregates
or an Access membership model. There are no cross-module relationships or business calls.

Both modules expose ordinary Npgsql options in their own consumer configuration. Each has
a native design-time factory, using the same provider/history configuration and an
uninitialized tenancy accessor: building a model must not resolve an operation tenant.
Scaffolding uses pinned local `dotnet-ef` 10.0.12 and private EF Design 10.0.12. It must not
execute the finite console or require a running database/personal credentials.

Reviewed initial migrations/snapshots stay in each module's folder, in the existing sample
assembly, with native context attributes distinguishing them. No empty migration projects
are added. Any generated annotation layout can be simplified only without changing native
migration/snapshot behavior. Runtime composition uses explicitly selected native `MigrateAsync`
calls; the host controls ordering and failures. Repeat invocation preserves both histories
and data. `EnsureCreated` remains only in the independent GUID fixture, which has no module
migration claim. Existing E2.1 Inventory tests now run against its real migrations.

## Focused proofs

- Model/migration ownership: actual Inventory and Sales migration operations and entity
  mappings target their own schemas/tables. Assert the supported operation schemas directly
  and fail on raw SQL/database-wide operations in these initial sample migrations instead
  of pretending to inspect arbitrary SQL. No synthetic test of xUnit's rejection is added.
- Real PostgreSQL: migrating Inventory alone creates neither Sales tables nor Sales history;
  migrating Sales afterward preserves existing Inventory rows and creates only its own
  objects. Distinct applied histories correspond to the intended contexts, with no public
  default history table. The symmetric module order is exercised if it adds relevant
  protection against a composition-order dependency.
- Console: first and repeat runs migrate both modules and print independently expected
  tenant-specific stock/customer results. This exercises the actual consumer entry point.
- Sales wiring: correctly owned rows save/read per selected tenant, and foreign ownership
  fails through both native consumer overrides. Do not repeat the full E2.1 helper matrix.
- Keep active architecture tests and the existing ownership proofs passing. Add no exact
  transitive dependency snapshots or tests of native migration internals in isolation.

## Review and limits

Review consumer options, design-time factories, migrations/snapshots, explicit initialization,
Sales ownership/save wiring and the narrow sample tests. Migrations are consumer-owned schema
changes; runtime users choose their execution environment and permissions. The finite demo
is for disposable databases. Existing E2.1 `EnsureCreated` databases need recreation rather
than an implicit upgrade path into migration history.

Shared cross-module transactions, same-tenant parent/child constraints, competing version
updates, HTTP ingress and richer business behavior remain later slices. Database schemas
do not provide hostile-code isolation or least-privilege database credentials. No new
reusable mechanism is proposed by this slice unless implementation evidence earns one.

Native references: [design-time creation](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation),
[history tables](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/history-table)
and [applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).
