# Applications

`apps` contains executable entry points. Business capabilities remain first-class siblings under [`modules`](../modules); directory placement does not reverse the dependency rule that applications compose modules and modules never depend on applications.

- `AppHost` is the conventional C# Aspire orchestration project. It starts PostgreSQL, runs the finite Migrator, and starts the API only after migrations succeed.
- `Api` is the composition root and HTTP entry point; it registers the modules and owns no business workflow.
- `Migrator` applies module-owned migrations in declared order under one PostgreSQL advisory lock and exits nonzero on failure.

The deferred Vite frontend will live at `apps/Web`; its module-specific screens remain feature folders inside that application until a real independent frontend package boundary is justified. [`shared/ServiceDefaults`](../shared/ServiceDefaults) contains only shared JSON logging, health, service-discovery, resilience, and OpenTelemetry setup.

## Module migrations

The Migrator owns only coordination. It calls each module's persistence-only registration entry point, so future API workers and message consumers cannot accidentally run in the migration job. Each module owns its `DbContext`, schema, history table, and migration files. The declared application order is Access, Inventory, Purchasing, then Sales; the PostgreSQL advisory lock prevents two Migrator processes from applying that sequence concurrently. The session-level lock uses one dedicated, non-pooled connection so it cannot starve the shared EF connection pool.

Before the first persistent local run, store a stable PostgreSQL password in Aspire's local secret store. The volume cannot be reopened if Aspire generates a different password on a later run:

```bash
aspire secret set Parameters:postgres-password '<strong-local-password>' \
  --apphost apps/AppHost/ModulithFoundry.AppHost.csproj
```

Local Aspire runs the Migrator as a finite project resource and gates API startup with `WaitForCompletion`. Deployment packages the same executable from the same revision as a one-shot job, waits for its successful exit, and only then rolls out the API. Production migrations are forward-only and expand/contract compatible with the previously running application; application rollback never automatically runs an EF down migration.

Restore the pinned repository tool before creating a migration. EF design-time discovery uses the Migrator as the startup project and therefore needs a syntactically valid connection string, but creating a migration does not connect to that database:

```bash
dotnet tool restore
ConnectionStrings__database="Host=localhost;Database=modulith_foundry" \
  dotnet ef migrations add MigrationName \
  --project modules/Access/Access/Access.csproj \
  --startup-project apps/Migrator/ModulithFoundry.Migrator.csproj \
  --context ModulithFoundry.Modules.Access.Persistence.AccessDbContext \
  --output-dir Persistence/Migrations
```

Substitute the owning module project and context. Never generate one module's migration into another module or the Migrator.
