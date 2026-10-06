# Wholesale consumer modules

The HTTP sample composes three populated module implementations through separate Contracts
projects. The native event-history executable also composes Purchasing. These are
consumer-owned sample/template projects, not reusable technical libraries.

| Module | Ownership | Persistence |
| --- | --- | --- |
| Access | Global application users, exact external identities, canonical Organizations and current-membership admission. | Schema `access`, native AccessDbContext, separate migration history; queries run before tenancy exists. |
| Sales | Versioned customer profile edits and one demonstration address per customer. | Schema `sales`, native SalesDbContext, separate history; explicit tenant ownership and native transactions. |
| Inventory | Availability reads, stock-position history, opening and receipt commands within the selected Organization. | Schema `inventory`, native InventoryDbContext, separate history; ordinary queries use the registered E2 ownership filter. |
| Purchasing | Purchase-order history, drafting and line changes, including replacement of a line for the same item. | Schema `purchasing`, native PurchasingDbContext, separate history; explicit tenant ownership. |

Each implementation references its own Contracts. None references a peer implementation
or the HTTP host. Contracts contain domain keys, query/command interfaces and immutable results;
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

## Event-history reads

[E5.2.1](../../../docs/plans/e5-2-1-native-event-history.md) adds stock-position history to
Inventory and introduces Purchasing/Purchasing.Contracts for purchase-order history.
Purchasing owns schema `purchasing`, a native PurchasingDbContext and a separate migration
history. Both module implementations explicitly adopt Events.Serialization, Events.History
and the existing EF tenant-ownership utility; neither uses messaging or ActorIdentity.

A stock position records opening at a stock-item/location pair and positive receipts in its
base unit, retaining the latest delivery reference. The read model reports on-hand quantity;
reservation/adjustment commands and durable business-key uniqueness are not implemented.
A purchase order records its code, supplier reference and currency, with positive line
quantities and nonnegative unit prices. A later line fact for the same item replaces that
line. The initial commands cover drafting and those line changes; a complete purchase-order
lifecycle is not implemented.

IStockPositionHistory and IPurchaseOrderHistory expose current, version and recorded-time
queries through immutable business results. Their implementation rows and codecs stay internal.
Public native contexts and finite HistorySeed helpers remain composition/setup exceptions,
not business Contracts or production append APIs. The caller establishes tenancy and saves.

Headers use `(organization_key, id)` primary keys; events use an owned event identity and an
owned stream-position unique index. A composite owner/stream foreign key prevents cross-tenant
references. Positive stream/event/schema versions are database constraints. Native migrations
and storage/ownership choices remain editable consumer/template source. [E5.3 registration](../../../docs/reports/e5-3-event-storage-registration.md)
now supplies the repeated technical mapping through an explicit call in each owning context;
there is no context discovery or automatic schema population. Existing snapshots/migrations
remain unchanged. Inventory's forward migration preserves its availability table.

[The native executable](../EventPersistenceDemo/README.md) exercises both modules independently
of HTTP and messaging. Its read consistency assumptions and error boundaries are documented
there. The existing HTTP application continues to use Inventory's state-stored catalog and
now applies the forward Inventory migration; it registers no history queries. Sales and Access
retain their existing persistence behavior. Owning Inventory's new event features introduces
its explicit event-library references; selecting only its catalog registration does not remove
those assembly dependencies.

## Explicit event commands

[E5.2.2](../../../docs/reports/e5-2-2-native-event-append.md) adds module-owned command decisions
and staging. IStockPositionCommands opens a position and stages positive receipt batches;
IPurchaseOrderCommands drafts an order and stages line batches. Their DTOs contain explicit
stream identity, business values and expected version, without tenant/actor or EF types.
Consumers register commands explicitly alongside history queries and native contexts.

Staged is proposed state only. The caller must start the module's native transaction before
command preparation, save and commit explicitly, and handle both preflight NotFound/Conflict
and save-time failures. Internal decisions deliberately produce typed events, reconstruct the
expected prefix, evolve and encode the complete proposal before changing tracked rows. Native
EF original-version predicates and owned keys arbitrate competing writers. Headers/envelopes
commit together; no worker lock, hidden retry, automatic event collection or publishing is added.

The supported recipe is one staged batch per stream per context. Native tracked headers detect
repeats before and after save; no separate command-lifecycle set is stored in the DbContext.
Manual clearing/detaching tracking mid-operation is outside this recipe. Different
streams can share the caller's native module transaction. Discard the whole context and proposal
after failure/rollback; a fresh operation may decide again. Public native AppendFailures helpers
recognize only stream-header concurrency and known owning-schema key/position constraints;
catalog concurrency and unrelated constraints remain faults.

Stock-item/location uniqueness and purchase-code uniqueness across different streams remain
unproven. Availability still is not an event projection. Required inline views/repair remain E6,
audit and messaging remain later explicit participants. No new Foundry library mechanism was
needed for these writes; the native setup and protocol are editable template candidates.
The later E5.3 utility extracts technical model registration only; it does not take over
command decisions, event collection, saving or transaction ownership.
