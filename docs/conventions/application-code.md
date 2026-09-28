# Application code conventions

These conventions keep module code navigable without introducing a mediator, generic application framework, or mandatory abstraction for every operation.

## Files and types

- Give each independently useful top-level type its own file with the same name, especially public Contracts types.
- Group a growing Contracts project into capability folders such as `Identity`, `Invitations`, and `Organizations`; folders need not fragment the Contracts namespace.
- Closely coupled internal implementation details may share their owner's file when separating them would make navigation worse. Generated files and deliberately grouped exception types are exempt.
- Name a file containing part of a partial type `{Type}.{Concern}.cs`.
- Give every enum member an explicit numeric value starting at `1`. Value `0` is reserved as an invalid/uninitialized state, including when the enum is persisted as text today.

## Composition and HTTP adapters

- A module exposes `Add{Module}Module` as its complete runtime registration entry point. A narrower registration such as `Add{Module}Persistence` exists only for a real host such as Migrator or a focused integration test.
- The API owns HTTP ingress adapters under `apps/Api/Modules/{Module}` and groups them behind `Map{Module}Api`.
- Reserve `Use...` for middleware pipeline behavior. Mapping routes is a `Map...` operation.
- Other modules, hosted workers, broker handlers, and durable processes call Contracts; they never call in-process HTTP endpoints.

## Commands and queries

Use pragmatic CQRS inside each module: writes and reads may share the module's database and DbContext, but their code and models have different responsibilities.

- One command handler executes one application operation and normally owns its transaction. Name it `{Verb}{Noun}Handler` and place it with that vertical operation, for example `CreateOrganization/CreateOrganizationHandler`.
- Query classes project read models directly and do not hydrate aggregates merely to display data. Name a cohesive query surface `{Subject}Queries` and place it under `Queries`; split it only when its size or distinct read models justify more files.
- Contracts expose explicit capability-oriented operations and read models. Do not introduce a generic command bus, mediator, marker interfaces, or a separate read store without an implemented need.
- Capability methods return operation-specific discriminated results for expected outcomes. Internal handlers may use implementation-local exceptions to protect domain construction, but expected failures do not escape through Contracts.
- Application handlers coordinate persistence and domain behavior. Business invariants remain on aggregates, value objects, or a domain service/policy when no one aggregate naturally owns the rule.

## Naming vocabulary

| Name | Meaning |
| --- | --- |
| `Handler` | Executes one application operation or use case. |
| `Queries` | Cohesive read operations for one subject or read model. |
| `Policy` | Makes a side-effect-free business decision. |
| `Resolver` | Resolves context or identity from supplied information. |
| `Factory` | Constructs a nontrivial domain object or aggregate. |
| `Provider` | Supplies an environmental or externally obtained value. |
| `Coordinator` or `Orchestrator` | Coordinates a multi-step workflow. Durable workflows also own durable process state. |
| `Repository` | An aggregate-specific persistence abstraction introduced only for a demonstrated need; never generic CRUD. |
| `Store` | A technical storage seam with multiple real adapters or another demonstrated substitution need. |
| `Service` | A cohesive domain capability that has no clearer domain name. Do not use it as the default suffix. |
| `Manager` | Avoid; it does not communicate a useful responsibility. |

Prefer a precise domain term over any suffix in this table.

## Reusable EF queries

- Module DbContexts expose internal, meaningfully named `DbSet` properties for mapped aggregate roots, technical records, and entities queried directly by projections. Module code uses those properties instead of ad hoc `Set<T>()`; this is a navigability convention, not an authorization boundary.
- Do not expose a child entity as a `DbSet` merely to mutate it independently. Querying a child table directly for a read projection is allowed when aggregate hydration would add no value.
- Extract reusable semantic predicates as internal `IQueryable<T>` extensions named `{Subject}QueryExtensions`, such as `Active`, `AccessibleTo`, or `Available`.
- Keep trivial one-off comparisons inline. A wrapper must add domain meaning or prevent meaningful rule duplication.
- Never expose `IQueryable` through Contracts.
- Do not use global query filters for ordinary lifecycle states that administrative queries must be able to see. Tenant-isolation filters are considered separately.

## Organization scope

- Organization-scoped HTTP endpoints live under `/api/o/{organizationSlug}` and opt into the API's organization-scope middleware through endpoint metadata. Do not parse path text, query parameters, or a mutable session preference to select the organization.
- Access resolves the authenticated product `UserId`, canonical route slug, and active `Membership` together before constructing the immutable `OrganizationAccessContext`. A missing or inaccessible organization fails closed without disclosing whether another tenant exists.
- `IOrganizationContextAccessor` is request/persistence infrastructure. Do not inject it into aggregates or use it to hide tenant selection from application operations; capability inputs and tenant-owned entities carry `OrganizationId` explicitly.
- Tenant-owned persistence types implement `IOrganizationOwned`. Their EF query filters deny access when no verified organization context exists. Access bootstrap queries such as organization choice and route resolution bypass only the named organization filter explicitly.
- Query filters are defense in depth, not authorization. Resource queries still constrain identifiers by `OrganizationId`, and application/domain policy still decides whether the current membership may perform an operation.
- Persistence never infers or fills `OrganizationId` from the current request. Constructors and handlers set it explicitly; persistence validation may reject missing or mismatched values.

## Mapping

Keep a private mapping method beside its only caller. When the same mapping has multiple callers or obscures an operation, move it to an internal `{Subject}Mappings` extension class. Contracts and transport DTOs never accept implementation entities in constructors.
