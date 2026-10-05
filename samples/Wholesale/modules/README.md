# Wholesale consumer modules

The HTTP sample composes three populated module implementations through separate Contracts
projects. These are consumer-owned sample/template projects, not reusable technical libraries.

| Module | Ownership | Persistence |
| --- | --- | --- |
| Access | Global application users, exact external identities, canonical Organizations and current-membership admission. | Schema `access`, native AccessDbContext, separate migration history; queries run before tenancy exists. |
| Sales | Versioned customer profile edits and one demonstration address per customer. | Schema `sales`, native SalesDbContext, separate history; explicit tenant ownership and native transactions. |
| Inventory | Availability reads for a SKU within the admitted Organization. | Schema `inventory`, native InventoryDbContext, separate history; ordinary queries use the registered E2 ownership filter. |

Each implementation references its own Contracts. None references a peer implementation
or the HTTP host. Contracts contain domain keys, read interfaces and immutable results;
they expose no EF, HTTP or technical context types. Rows and query implementations are internal.
Inventory stores the canonical Organization key without a cross-module database FK.

The [host](../HttpIdentityDemo/DemoComposition.cs) registers each native DbContext/provider
explicitly, calls `AddAccessQueries()` / `AddInventoryQueries()` / `AddCustomerProfiles()` to bind internal
implementations, and separately registers its HTTP resolvers. Business
[endpoints](../HttpIdentityDemo/Endpoints/OrganizationEndpoints.cs) call only business
Contracts. Actor/provider mapping, Organization admission metadata and selection helpers
live in the host's [HTTP integration](../HttpIdentityDemo/HttpIntegration/OrganizationTenantResolver.cs).

Public native DbContexts/configuration utilities are available to composition, native EF
tooling and finite setup. This exception keeps saves, migrations and transactions visible;
it does not make persistence types part of the business Contracts. Module seed helpers stage
rows only. Their caller establishes tenancy where needed and explicitly saves/commits.
Raw SQL and privileged native EF bypasses remain consumer responsibilities.

Inventory's `IStockCatalog.ReadAsync(sku, cancellationToken)` returns a result or null in the
current tenant. It accepts no caller-selected tenant, uses no filter bypass, saves nothing,
and propagates database faults/cancellation. Missing or tenantless context rejects access.
The schema permits the same SKU in multiple Organizations through an Organization/SKU unique
index. Availability is a small state-stored demonstration view; reservation commands and an
authoritative event-sourced stock model are not established here.

[Host setup and endpoints](../HttpIdentityDemo/README.md),
[the E3.4 plan](../../../docs/plans/e3-4-persisted-business-ingress.md) and
[the report](../../../docs/reports/e3-4-persisted-business-ingress.md) record adoption, proofs
and remaining limits. [E3.5](../../../docs/reports/e3-5-profile-mutation.md) adds the Sales mutation.

Sales's [Contract](Sales/Sales.Contracts/ICustomerProfiles.cs) returns a profile, or a typed
Updated/NotFound/Conflict outcome. Read and change require established tenancy. Change validates
IDs, text lengths and version range, uses the caller's expected version in the native UPDATE,
saves customer then address, and commits its own native transaction before returning Updated.
No actor/tenant is accepted from the request body. HTTP authorization and antiforgery remain
host obligations; trusted non-HTTP callers own admission. Discard the context after failure;
there is no automatic retry/rebase or shared ambient-transaction contract.

Materialized template/CLI output remains E10.
