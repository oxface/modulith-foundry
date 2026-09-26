# Applications

`apps` contains executable entry points. Business capabilities remain first-class siblings under [`modules`](../modules); directory placement does not reverse the dependency rule that applications compose modules and modules never depend on applications.

- `AppHost` is the conventional C# Aspire orchestration project. It starts PostgreSQL, Redis, and Keycloak; runs the finite Migrator; and starts the API only after its required resources are ready.
- `Api` is the composition root and HTTP entry point. It registers the modules and owns the OIDC/cookie adapter, but no business workflow.
- `Migrator` applies module-owned migrations in declared order under one PostgreSQL advisory lock and exits nonzero on failure.

The deferred Vite frontend will live at `apps/Web`; its module-specific screens remain feature folders inside that application until a real independent frontend package boundary is justified. [`shared/ServiceDefaults`](../shared/ServiceDefaults) contains only shared JSON logging, health, service-discovery, resilience, and OpenTelemetry setup.

## Module migrations

The Migrator owns only coordination. It calls each module's persistence-only registration entry point, so future API workers and message consumers cannot accidentally run in the migration job. Each module owns its `DbContext`, schema, history table, and migration files. The declared application order is Access, Inventory, Purchasing, then Sales; the PostgreSQL advisory lock prevents two Migrator processes from applying that sequence concurrently. The session-level lock uses one dedicated, non-pooled connection so it cannot starve the shared EF connection pool.

Before the first persistent local run, store stable development credentials in Aspire's local secret store. Persistent PostgreSQL and Keycloak volumes cannot be reopened if Aspire generates different administrative credentials on a later run. The OIDC client secret must match the value substituted into the imported local realm, and the seeded test user password is development-only:

```bash
aspire secret set Parameters:postgres-password '<strong-local-password>' \
  --apphost apps/AppHost/ModulithFoundry.AppHost.csproj
aspire secret set Parameters:keycloak-password '<strong-local-admin-password>' \
  --apphost apps/AppHost/ModulithFoundry.AppHost.csproj
aspire secret set Parameters:oidc-client-secret '<strong-local-client-secret>' \
  --apphost apps/AppHost/ModulithFoundry.AppHost.csproj
aspire secret set Parameters:keycloak-test-user-password '<local-alice-password>' \
  --apphost apps/AppHost/ModulithFoundry.AppHost.csproj
```

Interactive local runs expose PostgreSQL on `55432`, Redis on `56379`, Keycloak HTTPS on `58080`, API HTTP on `5080`, and API HTTPS on `5443`; topology tests randomize ports. Aspire supplies and propagates trust for its local developer certificate to Keycloak and the API. Run `aspire certs trust` before the first interactive start; on Linux, follow Aspire's output if the OpenSSL trust path must be added to `SSL_CERT_DIR`. The Keycloak Aspire hosting integration is a preview orchestration adapter isolated to `AppHost`; runtime authentication uses the standard ASP.NET Core OpenID Connect handler and has no Keycloak-specific production dependency.

Redis stores only Data-Protection-protected authentication tickets under a versioned application namespace. The browser receives an opaque secure cookie. Redis loss signs users out, Redis unavailability never falls back to a client-side ticket, and local Data Protection keys are separate from Redis. Production key-ring persistence remains a deployment concern described in ADR 0006.

`GET /api/session` returns the authenticated product identity plus a Data-Protection-backed CSRF request token. Cookie-authenticated state-changing requests send that value in the `X-CSRF-TOKEN` header; the browser never needs access to either the authentication ticket or the antiforgery cookie.

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
Raw SQL used for a necessary data backfill must declare its owning schema with the
`ModulithFoundry:OwnedSchema` migration-operation annotation; architecture tests reject
unannotated SQL and SQL attributed to another module's schema.
