# Application code conventions

These conventions keep module code navigable without introducing a mediator, generic application framework, or mandatory abstraction for every operation.

## Files and types

- Give each independently useful top-level type its own file with the same name, especially public Contracts types.
- Group a growing Contracts project into capability folders such as `Identity`, `Invitations`, and `Organizations`; folders need not fragment the Contracts namespace.
- Closely coupled internal implementation details may share their owner's file when separating them would make navigation worse. Generated files and deliberately grouped exception types are exempt.
- Name a file containing part of a partial type `{Type}.{Concern}.cs`.
- Do not split ordinary application/endpoint classes into partial files just to reduce file size. Prefer cohesive separate classes with explicit entry points; shared mappings belong in an actual mapping class, not private methods reached through another partial file. Partial types require a concrete reason, such as generated code or a deliberately grouped test fixture.
- Give every enum member an explicit numeric value starting at `1`. Value `0` is reserved as an invalid/uninitialized state, including when the enum is persisted as text today.

## Composition and HTTP adapters

- A module exposes `Add{Module}Module` as its complete runtime registration entry point. A narrower registration such as `Add{Module}Persistence` exists only for a real host such as Migrator or a focused integration test.
- The API owns HTTP ingress adapters under `apps/Api/Modules/{Module}` and groups them behind `Map{Module}Api`.
- Reserve `Use...` for middleware pipeline behavior. Mapping routes is a `Map...` operation.
- Other modules, hosted workers, broker handlers, and durable processes call Contracts; they never call in-process HTTP endpoints.
- Prefer command-shaped endpoints such as `suspend`, `reactivate`, or `remove` when the caller is requesting a domain action. Use generic update or replacement endpoints only when the operation genuinely replaces user-editable state rather than concealing a business command behind field mutation.

## Commands and queries

Broker ingress lives in a concrete module-owned messaging adapter, named `{Command}MessageHandler`. It owns Rebus interfaces, ambient message headers, envelope matching and transport/host cancellation. It calls the ordinary application handler with a typed input and cancellation token; the application handler retains payload/business validation and its transaction without reading `MessageContext`. Do not introduce a generic envelope/pipeline library until repeated real adapters justify extraction. Producer headers are consistency checks, not authentication.

Use pragmatic CQRS inside each module: writes and reads may share the module's database and DbContext, but their code and models have different responsibilities.

- One command handler executes one application operation and normally owns its transaction. Name it `{Verb}{Noun}Handler` and place it with that vertical operation, for example `CreateOrganization/CreateOrganizationHandler`.
- Query classes project read models directly and do not hydrate aggregates merely to display data. Name a cohesive query surface `{Subject}Queries` and place it under `Queries`; split it only when its size or distinct read models justify more files.
- Contracts expose explicit capability-oriented operations and read models. Do not introduce a generic command bus, mediator, marker interfaces, or a separate read store without an implemented need.
- Name capability interfaces for their actual responsibility, not merely a singular/plural entity. Prefer `I{Subject}{Responsibility}`, such as `IOrganizationQueries` or `IStockPositionProjectionRebuilder`. A combined surface may use `Operations` when that accurately describes it; do not mechanically append `Operations`, `Manager`, or `Service` to every interface. Use `Handler` for one application use case, `Queries` for reads, and a precise task name such as `Rebuilder` or `Resolver` when it communicates the capability.
- A data noun is not a capability responsibility: use `IStockItemReferenceResolver` / `StockItemReferenceResolver`, not `IStockItemReferences` / `StockItemReferences`. Keep `StockItemReference` for returned data. Apply the same responsibility name to the interface, implementation, filename and test decorators.
- Capability methods return operation-specific discriminated results for expected outcomes. Internal handlers may use implementation-local exceptions to protect domain construction, but expected failures do not escape through Contracts.
- Persisted-state and event-store integrity faults use specific module-local exceptions with safe diagnostic identity/reason/version fields; they are not expected business rejection results or automatic-retry requests. Production HTTP returns the existing generic 500 Problem Details with trace ID. Keep `InvalidOperationException` for programming misuse and composition failures; do not globally classify every such exception as data corruption.
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
- Separate DbSet properties with a blank line. CSharpier checks and preserves layout but does not insert this member separation automatically; the pre-commit formatter check is not a substitute for this readability convention.
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
- Access and Inventory apply the shared EF ownership-filter utility after entity configuration. Each DbContext supplies an expression tied to its own current organization; the utility has no dependency on module Contracts. It applies filters to mapped root entity types implementing the ownership interface. Derived entities inherit the root filter; EF-owned child types inherit their owner's query scope.
- Query filters are defense in depth, not authorization. Resource queries still constrain identifiers by `OrganizationId`, and application/domain policy still decides whether the current membership may perform an operation.
- Persistence never infers or fills `OrganizationId` from the current request. Constructors and handlers set it explicitly; persistence validation may reject missing or mismatched values.

## Mapping

Keep a private mapping method beside its only caller. When the same mapping has multiple callers or obscures an operation, move it to an internal `{Subject}Mappings` extension class. Contracts and transport DTOs never accept implementation entities in constructors.

## Persisted event contracts

- Persisted event payloads declare a stable semantic alias and positive schema version through the module-local `StoredEventType` attribute. CLR names and namespaces do not determine storage identity. The same validated registry drives serialization and deserialization; missing or duplicate identities fail during composition.
- Related internal event payloads may share one `{Aggregate}Events.cs` file. Distinct incompatible payload versions may use `V1`/`V2` CLR suffixes while retaining the semantic alias and separate stored schema version.
- Additive fields may retain a schema version only when missing-field defaults preserve historical meaning and compatibility fixtures prove hydration. Incompatible changes require an explicit new version and a supported read path for retained old events. Introduce transformations only when exercised; never rewrite immutable history as ordinary schema evolution.
- A decider proposes a complete event batch. Evolve and validate candidate state before changing aggregate state, version, or pending events. Shared domain policies belong beside the decider when actual invariants justify them. Replay applies recorded facts without rerunning current decision policies or publishing effects.
- Validate final candidate state after the complete decision batch, not each intermediate state. New input construction validates values; trusted restoration and recorded evolution preserve facts without current input/business validation. Timeline mappings preserve stored explanatory text rather than reconstructing it through today's validated input factories. Schema/stream integrity checks remain separate. Leave repeated handler authorization explicit until a real extraction proves a smaller solution; any future decorator/pipeline must cover contract callers as well as HTTP, and does not mandate a mediator.
- Event-store mechanics own expected-version checks, event envelopes, metadata construction, and database concurrency classification. Handlers retain transaction ownership, business orchestration, and operation-specific results/audit.
- Stream IDs are durable technical identity. An aggregate-shaped inline write model can share write-state evolution with live reconstruction and commits with the append; other views can use different reducers. Whether business-key lookup relies on that required projection is an explicit operational choice, not a reason to add business fields to a generic stream header.

### Event-sourcing vocabulary and responsibilities

- **Aggregate** is the business consistency boundary. **Decision state**, named `{Aggregate}State` (for example, `StockPositionState`), is complete data for its decider regardless of loading lifecycle. The recommended load-for-writing path uses an aggregate-shaped inline write model; live reconstruction remains available. The immutable decision state is independent of EF tracking. Only evolution advances event-sourced state; commands do not separately mutate quantities and emit corresponding events.
- Keep deciders and policies internal. Expose advisory action availability through capability Contracts only when needed, distinguishing general availability from eligibility for supplied command inputs. Reuse pure action-specific evaluation between query and decision rather than duplicating rules; commands always recheck authorization and eligibility. Resulting-state invariant validation is a separate responsibility. Do not add a generic policy framework or availability surface without a concrete caller.
- **Projection** is an event-derived representation; **projector** is the code that constructs it. A **read model** is shaped for queries. Write state and read models may share a shape, but neither must mirror the other.
- **Live aggregation / hydration** computes state on demand from ordered events. **Inline projection** persists an event-derived model in the append transaction; failure rolls back the append. **Asynchronous projection** advances separately after events commit and may lag. C# `async`/`await` does not determine a projection lifecycle. Avoid the ambiguous term “sync projection” when same-transaction atomicity is intended.
- **Snapshot** means a separate versioned hydration checkpoint. Inline projections are continuously maintained models, including the recommended aggregate-shaped write model; they are not prohibited by the deferred checkpoint-snapshot policy. Marten's broader use of “snapshot” must not obscure this repository distinction.
- The Stock Position aggregate does not retain an original-state copy for projection staging. Its projector receives stream identity, expected version, and accepted events, loads its own persisted state, and validates its version before evolution. Missing or lagging inline state is an integrity fault requiring repair; concurrently advanced state is a version conflict. No write silently repairs projection state.
- The current quantity projection shares the pure domain `Evolve` function while its shape matches the write state. A future differently shaped projection can have its own deterministic reducer; it must not introduce business decisions or publish replay effects.
- `EventStream` remains a domain-neutral header; Stock Item/Location fields belong in Inventory models, not generic stream metadata. A JSONB write document with typed technical columns and targeted business-key indexes is a proposal to verify, not a blanket storage rule.

The [consolidated event-sourcing direction](../plans/event-sourcing.md) records explicit projection/preview/rebuild operations, upcasting, manual transactional integration publication, deferred async workers, and the second-aggregate/extraction/scaffolding sequence. These are design targets, not a claim that all utility or lifecycle choices are already implemented.

These distinctions follow Marten's [projection roles and lifecycles](https://martendb.io/events/projections/) and [aggregate projection terminology](https://martendb.io/events/projections/aggregate-projections.html); they do not introduce a Marten dependency.
