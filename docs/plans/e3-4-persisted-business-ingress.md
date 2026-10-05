# E3.4 persisted business ingress and module projects

Status: owner-approved scope and project boundaries. E3.3 was owner-reviewed and checkpointed
as `20a02be`. Implementation is complete for owner review, with changes unstaged and no commit
authorized. [The report](../reports/e3-4-persisted-business-ingress.md) records actual results
separately from the obligations below. No new reusable mechanism was extracted.

## Outcome and review boundary

Connect the admitted HTTP operation to an Inventory-owned PostgreSQL catalog using the
existing E2 ownership utility. Replace the fixture quantities with actual tenant-owned
rows. Deliver meaningful Access and Inventory implementation/Contracts projects alongside
this capability, so the executable host demonstrates module composition and business calls
through Contracts. Keep the existing `HttpIdentityDemo` executable as the aggregating host.

This increment is read-only at HTTP ingress. Native cookie antiforgery and mutations need
their own review, including concurrency, authorization timing and explicit saves. E3.3's
admission-time revocation policy remains in force; a read does not strengthen it into
transaction-time authorization.

The library implementations are expected to remain unchanged. This increment earns an
editable module/persistence/endpoint setup pattern for the template. Materialized template
output and the configurable bootstrap CLI remain E10, using these exercised source files.

## Consumer ownership and proposed projects

Introduce only projects with actual responsibilities:

| Project under `samples/Wholesale/modules/` | Responsibility |
| --- | --- |
| `Access/Access.Contracts` | Existing global user/provider identity and Organization admission read Contract and value types. |
| `Access/Access` | Existing registry queries, canonical slug policy, internal rows, native DbContext and migrations. |
| `Inventory/Inventory.Contracts` | Asynchronous stock-availability read Contract and result. |
| `Inventory/Inventory` | Tenant-owned catalog rows, EF query implementation, native DbContext and migrations. |

The sample namespaces become `ModulithFoundry.Samples.Wholesale.Access[.Contracts]` and
`ModulithFoundry.Samples.Wholesale.Inventory[.Contracts]`; they are consumer modules, not new
technical packages under `src/`. Contracts expose neither EF/HTTP types nor actor/tenant
context types. Implementation types and entity rows stay internal.

Move the current actor resolver, Organization resolver, public-access metadata and selection
registration into a host-owned HTTP integration folder. These bridge native authentication,
technical contexts and Access Contracts; they do not belong inside the Access domain module.
Split the current `AddApplicationAccess` responsibilities: the host explicitly registers
its native DbContext/provider options, the module provides a small registration helper for
its internal Contract implementation, and the host registers its HTTP resolver separately.
Apply the same visible composition to Inventory. No discovery or automatic model population.

Business endpoints and HTTP resolvers call module Contracts. Put business endpoint handlers
in types separate from composition/initialization so their dependency boundary is explicit.
The host's composition and
finite setup code may reference the module's public native DbContext/configuration surface
to configure options, migrate and explicitly save. This is a proposed composition exception
to the business-call boundary, not permission for endpoints to query module rows. Keep that
surface small and review it together with the module project structure; avoid wrapping
native EF transactions in a custom persistence abstraction just to hide the DbContext.

No cross-module implementation reference or database FK joins Inventory to Access.
Inventory stores the canonical Organization key as its tenant discriminator. Access owns
admission; Inventory owns business-data isolation. Technical libraries acquire no sample
module references. Existing finite ContextDemo/PersistenceDemo consumers remain independent;
do not reference their executables as module libraries or relocate their historical proofs.

## Proposed business read

Replace the synchronous fixture Contract with:

```csharp
Task<StockAvailability?> ReadAsync(
    string sku, CancellationToken cancellationToken);
```

Keep `StockAvailability(string Sku, int AvailableQuantity)` as the consumer-owned result.
A null result means this SKU is absent in the admitted Organization. Validate nonblank SKU
at the consumer boundary; preserve exact SKU comparison, with no speculative normalization
policy. No `IQueryable`, EF entity, caller-supplied tenant key, mediator or generic repository
crosses the Contract. Pass request cancellation to the native async query.

Preserve the existing public `/organizations/{organization}/catalog` endpoint and response
shape. An optional `sku` query parameter selects the SKU, defaulting to the demonstration
`DEMO-NOTEBOOK`. Add a protected business read at
`/organizations/{organization}/stock/{sku}` using the same Contract, with native
`.RequireAuthorization()` and ordinary active-membership admission. The public endpoint
retains `.AllowAnonymous().AllowPublicOrganizationAccess()`. Equivalent subdomain-host test
routes exercise the same capability; URL selection remains a host registration choice.

Return native ProblemDetails 404 for an absent SKU after successful admission. Preserve the
generic Organization-denial response before endpoint work. Database faults propagate to
the existing 500 handler; cancellation never returns fixture data or an unscoped result.
Actor/tenant diagnostic responses remain sample witnesses, not module business Contracts.

The catalog is a deliberately small state-stored availability view. This increment does
not establish stock reservation rules, quantity mutation commands or the authoritative
event-sourced stock-position model planned in E4. Review that domain/storage choice when
those behaviors are introduced; do not introduce competing authoritative models now.

## Persistence and explicit setup

Use one shared database with separate native contexts and histories:

| Module | Schema | Migration history | Scope |
| --- | --- | --- | --- |
| Access | `access` | `access.__EFMigrationsHistory` | Global identity/admission registry; unchanged scope semantics. |
| Inventory | `inventory` | `inventory.__EFMigrationsHistory` | Tenant-owned catalog rows. |

Inventory's table has a row key, required Organization key, required SKU and available
quantity. A unique `(OrganizationKey, Sku)` permits the same SKU in different Organizations.
Use the existing explicit `HasTenantOwnership` registration with a current-context tenant
expression and a named ownership filter. Explicitly call `ValidateTenantChanges` in native
save overrides. No soft deletion, extra lookup tables or aggregate framework is needed for
this read capability. Query through ordinary scoped EF, without filter bypass or a manually
selected tenant predicate as a substitute for the registered filter.

The required tenant comes from `ITenantContextAccessor.Current.RequireTenant()` for the
operation. Access reads precede publication; Inventory reads follow it. Inventory context
construction/model building must not demand tenancy before an actual tenant-owned query or
save. A missing/tenantless context fails business access rather than exposing all rows.
Contexts remain request scoped, with no implicit save, retry or shared transaction.

Use native EF migration scaffolding and review the resulting C#. Moving Access to a module
must preserve its existing migration ID, schema, keys and history compatibility. Do not
replace the Access migration, recreate its data or require a fresh database merely because
its CLR namespace/project changed. Inventory adds its own initial migration.

Keep `--initialize-access` as the existing Access-only finite mode. Add an explicit
`--initialize-demo` mode which migrates both modules, then stages and saves demonstration
rows with native transaction/commit calls visible in setup code. Setup needs no OIDC
configuration and exits without starting HTTP. A fresh disposable database remains the
supported demonstration-seed target; no general provisioning/reconciliation CLI is added.
Ordinary host startup neither migrates nor seeds either module.

Seed Inventory through explicit tenant-scoped contexts, one Organization at a time; do not
disable the ownership filter or validation for convenience. A small sample-only staging
helper may construct internal rows, but the caller owns save/transaction/commit. No atomic
cross-module setup or business-transaction guarantee is claimed. Use a shared connection
configuration while configuring each module's native schema/history explicitly. The host
can pass its existing `ConnectionStrings:Access` value to both registrations in this increment;
the modules receive native options, not a mandatory configuration-key convention.

## Executable proofs

Extend the existing real PostgreSQL 18.6 Testcontainers and native TestServer/cookie consumer
suite; no second fixture-backed HTTP host. Preserve the useful E3.3 admission cases against
the new persisted catalog, adapting setup and async endpoints. Add only witnesses needed
for this integration:

| Proof | Observable evidence |
| --- | --- |
| Actual persisted reads | Store `DEMO-NOTEBOOK` in Alpha/Beta with quantities 42/7; the same actor admitted to both receives the correct row. Change a backing quantity explicitly and a fresh HTTP read observes it. |
| Tenant isolation | A SKU present only in Beta returns 404 in Alpha and succeeds in Beta, including public reads. Alternate/overlap requests through the same cached EF model without leaking rows or context between Organizations. |
| Protected business admission | Active member reaches the persisted stock endpoint. Missing/suspended/removed membership denies before Inventory query work; revocation blocks the next request with the same cookie. Anonymous callers retain the native challenge. |
| Explicit public exception | Anonymous and mapped non-member public reads see only the selected Organization's catalog. That public capability does not grant protected stock access. |
| Fail closed | An Inventory database fault returns 500 and a fresh request succeeds after repair. Cancelling a real pending catalog read produces no successful result/fallback. A direct Contract call without established tenancy fails. |
| Module migration compatibility | A database created from the checkpointed Access migration and containing known rows is recognized by the relocated module; Inventory migration preserves them and uses a separate history. No recreation or extra Access migration is needed. |
| Explicit setup | Finite full-demo setup creates both schemas and exits; the ordinary HTTP host reads its rows. Starting normally against an empty database creates neither schema. |
| Project boundary | Focused ArchUnitNET checks on the real module/host assemblies prevent implementation-to-implementation dependencies, EF/HTTP leakage into Contracts, and business endpoints depending on persistence implementation types. |

Use independent expected keys, quantities and absent-SKU witnesses. Denied-admission tests
can make Inventory unavailable and still observe denial, showing business work did not
start without production timing hooks. Cancellation uses a real PostgreSQL lock and an
autocommit observer, following E3.3's corrected proof. Avoid repeating framework behavior,
synthetic architecture violations, custom restored-project graphs or a generic migration
parser. Internal visibility already enforced by C# needs no separate test matrix.

Keep native migration compatibility evidence tied to a frozen checkpoint fixture or
explicit unchanged migration metadata/SQL witnesses; re-running only the relocated current
model cannot by itself demonstrate compatibility with the checkpoint database. Any new
fixture belongs to active tests, not a modification of archived sources.

Run the adapted PostgreSQL HTTP suite, relevant existing E2 PostgreSQL consumer suite,
standalone library/adapter/context suites and architecture checks. Verify active build,
style/analyzers, formatting, archive integrity and local links. Update solution, CI and
developer commands for actual new projects. Report fresh counts and distinguish historical
E2/E3 evidence; real-provider, broker and Aspire topology remain unproven here.

## Findings and remaining work

**Library:** exercise the existing EF ownership utility with the established HTTP tenancy
seam. No new abstraction or reusable mechanism is assumed. If integration exposes a concrete
library gap, stop and propose its smallest interface separately for owner review.

**Template:** establish populated module/Contracts projects, host-owned HTTP bridges, explicit
native registration, separate histories and finite setup as executable editable recipes.
The final template materialization still happens in E10.

**Sample:** Access owns global identity/admission; Inventory owns tenant-isolated availability
reads. Keep business rules, provider settings, metadata and response policy consumer-owned.
Record the reviewed module-composition boundary in an ADR only after it is settled.

E3 remains open after this slice. Subsequent bounded reviews cover a state-changing business
journey with native antiforgery/concurrency/explicit save, and actual provider/session/proxy
topology with Aspire ServiceDefaults. Membership administration, roles, automatic linking,
tenant-aware native authorization and shared module transactions are separate capabilities,
not prerequisites for this read slice. E4's event-sourcing work follows the reviewed ingress
foundation; this plan does not claim that later scope complete.

Leave implementation changes unstaged for line-by-line owner review. Plan approval authorizes
implementation, not a commit.
