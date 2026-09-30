# Modulith Foundry: Architecture and Delivery Plan

Status: Accepted architecture baseline.

Last reviewed: 2026-09-23

Companion evidence:

- [Technology baseline research](../research/2026-09-23-technology-baseline.md) records the time-specific versions, licenses, lifecycle policies, and primary sources behind the initial technology proposals.
- [Azure runtime and operational options](../research/2026-09-23-azure-runtime-and-operational-options.md) evaluates Azure cost and spend controls, gateways, brokers, BFF state, secrets, Entra, Aspire testing, k3s/Flux, Marten patterns, MAF, and licensing in response to the first review.
- [Cheapest feasible Azure footprint](../research/2026-09-23-cheapest-feasible-azure-footprint.md) compares the lowest-cost local experiment, Azure pilot, and production-capable shapes without hiding their reliability limits.
- [Rebus sagas and tenant routing](../research/2026-09-23-rebus-sagas-and-tenant-routing.md) examines durable workflow ownership, Rebus persistence boundaries, and bookmarkable multi-organization routing.
- [Rebus idempotency and delivery semantics](../research/2026-09-23-rebus-idempotency-and-delivery-semantics.md) audits exact Rebus, RabbitMQ, and Azure Service Bus responsibilities and the remaining application idempotency boundary.
- [Rebus endpoint topology](../research/2026-09-23-rebus-endpoint-topology.md) compares a host-wide queue with module-owned endpoints, verifies Rebus multi-bus handler isolation behavior, and evaluates the extraction path.
- [Authorization and OpenFGA](../research/2026-09-23-authorization-and-openfga.md) compares native product authorization with a relationship-graph service and defines concrete adoption triggers without adding a v1 dependency.
- [Identity-provider invitation and JIT flow](../research/2026-09-23-identity-provider-invitation-and-jit-flow.md) verifies the portable boundary between product-owned invitations and Keycloak/Entra self-service identity creation.
- [OIDC onboarding modes and a later OpenFGA mapping](../research/2026-09-23-oidc-onboarding-modes-and-openfga-mapping.md) shows how the same product flow supports open and directory-gated identity providers and how static system roles could later map to an OpenFGA model.
- [V1 scope and deferred register](./v1-scope.md) applies YAGNI across every discussed capability and records the trigger for anything intentionally moved out of v1.
- [Earlier attempts](./earlier-attempts.md) maps the reported dead ends to explicit countermeasures and proof slices.
- [Module charters](../modules/README.md) record ownership, interfaces, invariants, authorization, and exclusions for the four business modules.
- [V1 delivery slices](./v1-slices.md) turns this roadmap into review-sized vertical increments with acceptance evidence.

## 1. Purpose

Build one concrete .NET modular-monolith example, deploy it as one application, and only then use what was learned to create a separate real product by copy/rename or an agent-assisted recipe. This repository is not intended to become a reusable application framework.

Before the first product delivery, prefer the cleanest understood design over preserving accidental compatibility with an earlier implementation. Decisions in this plan are reviewable and may be superseded when a failure test, product requirement, or simpler model disproves them. Reworking an established part is acceptable before v1; carrying a known dead end forward is not.

### Reversibility before v1

- **Decided does not mean irreversible.** It identifies the current implementation baseline so work can proceed without reopening every choice by default.
- Implementation evidence outranks this document. If code shape, failure tests, operability, or comprehension expose a poor design, stop, update the decision and tests, and rebuild the affected part cleanly rather than layering compatibility or adapters over a pre-v1 mistake.
- Before any real product data or external consumer exists, migrations may be regenerated, persisted-event/message fixtures deliberately replaced, APIs renamed, and abstractions removed when that produces the cleaner v1. Record the reason and supersede affected ADRs instead of pretending the earlier decision was never made.
- After a real deployment establishes durable data or external contracts, changes require explicit data migration, event/message compatibility, rollout, and rollback plans. The freedom to rewrite remains, but the compatibility cost becomes part of correctness.
- Tests should protect required behavior and architectural boundaries, not incidental class arrangements. A test suite that prevents a justified cleanup for no user-visible or operational reason should be changed with the design.
- Sunk implementation effort is never, by itself, a reason to retain a known dead end.

### Delivery and approval — **Decided**

Pull requests are self-contained coherent outcomes sized for reasonable review rather than maximum throughput. A v1 slice may span several independently valid vertical pull requests. Every commit requires the repository owner's explicit approval of the exact proposed change set; permission to implement or approval of the plan is not permission to commit. The authoritative policy is [Repository workflow](../conventions/repository.md).

The plan uses five status labels:

- **Decided** — the accepted current baseline for the first implementation; replaceable when implementation evidence justifies superseding it.
- **Proposed** — recommended default, awaiting review.
- **Open** — a choice or missing fact that blocks some later work.
- **Deferred** — recorded with an adoption trigger but deliberately creates no code, abstraction, persistence, or infrastructure now.
- **Risk** — a condition to test or control explicitly.

YAGNI applies to implementation, not to architectural memory: record plausible later concerns and their trigger, but build only behavior exercised by a current workflow or required operational property.

## 2. Current baseline

### Repository

- **Decided:** The repository is Apache-2.0 licensed.
- The approved architecture/research baseline and repository-local agent guidance govern the review-sized implementation increments.

### Development VM

| Capability | Observed state | Planning consequence |
| --- | --- | --- |
| OS | Ubuntu 26.04 LTS, Linux x64 | Supported local Linux target |
| Compute | 8 CPUs, 23 GiB RAM, 84 GiB free disk | Adequate for the proposed local containers |
| .NET | SDK 10.0.112; runtime and `dotnet-ef` 10.0.12 | Pin the repository to the accepted .NET 10 SDK feature band in Increment 1.1 |
| Aspire | CLI 13.5.4 stable | Suitable local orchestrator for the conventional C# AppHost |
| Containers | Rootless Podman 5.7.0, cgroups v2 | Use Aspire's detected Podman runtime |
| Node | Node 24.21.0, npm 12.0.2 | Available if the test frontend requires it |
| Missing CLIs | Docker, `psql`, `kubectl`, Flux, Java | Not blockers for planning; install only when a phase needs them |
| Aspire doctor | 4 pass, 2 warnings, 0 failures | Fix developer certificate trust before browser/OIDC work |

The Podman host already runs containers belonging to other Aspire applications. Their random host ports avoid immediate conflicts; this repository must use its own isolated Aspire session and must not operate those resources.

## 3. Goals and non-goals

### Goals

1. Demonstrate at least three meaningful business modules through one real end-to-end workflow.
2. Make module ownership visible in code, persistence, migrations, tests, telemetry, and operational runbooks.
3. Prove the chosen consistency mechanisms under failure, not merely on the success path.
4. Produce one deployable application image and deploy it to a selected environment.
5. Extract a low-ceremony process that can create the separate product repository without carrying example-domain debris.

### Non-goals

- A general modular-monolith framework, internal platform, or source generator.
- HTTP or broker calls between modules in the same process when a direct contract call is sufficient.
- Event sourcing every module by default.
- Kubernetes or GitOps artifacts before the deployment target and operating model are chosen.
- Redis/Valkey, messaging, a BFF, or any other infrastructure without a demonstrated product use case.

## 4. Architecture decisions already made

### AD-01 — Runtime and deployment shape — **Decided**

One application process and one application deployment contain at least three business modules. Modules are not HTTP microservices.

### AD-02 — Module persistence ownership — **Decided**

Use one PostgreSQL database. Every module owns a PostgreSQL schema, its EF Core persistence code, and its migrations. A module must not read, write, map, join, or declare foreign keys against another module's tables.

Identifiers may cross a module seam as opaque values, but referential validity across schemas is enforced by the owning use case rather than a cross-schema database constraint.

### AD-03 — Project and reference rule — **Decided**

Start with two production projects per module:

```text
modules/<Module>/<Module>.Contracts
modules/<Module>/<Module>
```

Other modules may reference only `<Module>.Contracts`. The API may reference module implementations solely to compose the process. Architecture tests enforce project references and forbidden exposed types.

The repository uses a monorepo-oriented top-level taxonomy: `apps` contains executable entry points, `modules` contains backend business capabilities, `shared` contains narrowly justified technical infrastructure, and `tests` contains .NET and system-level verification. The deferred frontend belongs in `apps/Web`; module-specific views begin as feature folders inside that application rather than packages beside the backend modules.

### AD-04 — API responsibility — **Decided**

The API is the composition root and HTTP entry point. It owns process-level routing, authentication wiring, shared infrastructure registration, and hosted-worker activation. Business workflows stay in their owning business module, not in the API.

### AD-05 — In-process collaboration — **Decided**

Modules invoke small contract interfaces in process for queries and commands. Contracts do not expose entities, `DbContext`, `IQueryable`, persistence transactions, or storage details.

### AD-06 — Two consistency modes — **Decided**

- A short workflow that truly needs all-or-nothing changes may use an explicit shared PostgreSQL transaction, but only after its rollback behavior is proven by integration tests.
- Workflows involving separate commits, external effects, or long waits use durable orchestration, retries, idempotency, and compensation where appropriate.

### AD-07 — Event types — **Decided**

Domain events are internal facts used within an owning module. Integration events are explicit, versioned contracts for durable communication outside that transaction/module. They are not the same types and do not share persistence semantics accidentally.

### AD-08 — Design style — **Decided**

Use DDD terminology where the domain earns it and organize implementation by vertical slice. Interfaces at module seams should be deep: small capability-oriented contracts hiding substantial implementation detail.

## 5. Product discovery and module map

### Gate G0 — Product definition — **Decided; closes with Phase 0 approval**

The reference product is a multi-tenant wholesale-operations ERP. Its first valuable workflow takes an authenticated Organization member from stocked-goods setup through Sales Order submission, independent approval, durable per-line reservation or shortage-driven replenishment, and cancellation compensation.

The [module charters](../modules/README.md), [domain glossary](../domain/CONTEXT.md), and [delivery slices](./v1-slices.md) define the actors, owned decisions/data, invariants, external effects, human waits, deadlines, audit baseline, and explicit exclusions. Detailed ERP completeness and unknown compliance policy are not scaffolding gates; they retain adoption triggers in the deferred register.

Do not create placeholder `Orders`, `Customers`, and `Payments` modules merely to satisfy a module count. A bad context split is harder to remove than a missing abstraction.

### Initial business modules — **Decided**

- **Access** owns organizations, external-identity links, memberships, and the product authorization model.
- **Sales** owns customers, sales orders, and the Order Fulfilment Process.
- **Inventory** owns minimal Stock Item definitions, stocking locations, stock positions, reservations, releases, and stock movements. A Stock Item has a stable identity, customer-provided SKU, description, base unit, and active status; richer catalog behavior does not justify a separate module in v1.
- **Purchasing** owns replenishment requirements, suppliers, and purchase orders.

Attachment bytes use shared Blob infrastructure, while business ownership, authorization, metadata, and audit remain with the module whose record carries the attachment. Do not create generic Files or Orchestration business modules initially. The accepted responsibilities and first-workflow invariants are in the [module charters](../modules/README.md) and [v1 delivery slices](./v1-slices.md).

Sales validates Stock Items through `Inventory.Contracts` and keeps the immutable SKU/description/unit snapshot required to understand each order line. Purchasing references the stable Stock Item identity while owning supplier-specific sourcing data. Neither module reads Inventory tables, and no shared product table is introduced. Reconsider a Catalog module only when a real capability such as non-stocked products, variants, merchandising, product hierarchy, or independently owned rich product content appears.

Inventory also owns a minimal Stocking Location with stable identity, organization-scoped customer code, name, and active status. Warehouse zones, bins, routing, capacity, and transfer workflows are outside v1.

### Module responsibility template — **Decided**

For each chosen module, record a one-page module charter:

| Field | Required content |
| --- | --- |
| Purpose | Business capability and decisions it owns |
| Owned concepts | Aggregates/entities/value objects in its ubiquitous language |
| Owned data | Schema name, authoritative records, retention |
| Contract commands | Intent, required caller knowledge, errors, idempotency |
| Contract queries | Stable read models returned; latency/consistency expectations |
| Integration events | Facts published after commit and their compatibility policy |
| Consumed events | Why asynchronous handling is correct and how it is idempotent |
| Invariants | Rules enforced in the module |
| Authorization | Business permissions enforced behind the interface |
| Explicit exclusions | Related responsibilities owned elsewhere |

Each module implementation remains one project organized primarily by vertical feature, with internal `Domain`, `Persistence`, `Messaging`, and `Composition` areas where they clarify ownership. Do not split Domain/Application/Infrastructure into additional projects; the `{Module}.Contracts` boundary, internal visibility, namespaces, and architecture tests provide the intended isolation.

### Dependency rules — **Decided**

1. A module implementation references its own Contracts project and may reference another module's Contracts project.
2. A Contracts project references no module implementation, application, EF Core, ASP.NET Core, Rebus, or vendor-specific persistence package. It may reference another Contracts project only for stable owner-defined identifiers/value types genuinely present in its interface; Contracts dependency cycles and transitive DTO graphs are forbidden.
3. Contracts contain capability interfaces, request/response records, stable IDs/value types, documented errors, receiver-owned durable integration-command schemas, and producer-owned integration-event schemas only. Contracts contain no Rebus types or attributes.
4. Domain events, aggregates, handlers, endpoint implementations, EF mappings, migrations, and message handlers remain in the implementation project and are internal by default.
5. The API references all module implementations for registration and route mapping, but no module DbContext or repository is resolved or used by API code.
6. Endpoint code lives with its vertical slice inside the module. The API calls one composition extension per module to register and map it.
7. A very small shared building-block project is allowed only after two modules need the same stable concept. It must not become a shared domain model.

Architecture tests should detect:

- forbidden project/assembly references;
- cyclic or unjustified Contracts-to-Contracts references;
- public EF/ASP.NET/Rebus types in Contracts;
- public domain and persistence types from implementation assemblies;
- API types depending on module feature namespaces;
- one module's EF model or migrations mentioning another module's schema;
- contract DTOs returning `IQueryable`, entities, or infrastructure abstractions.

The compiler/project graph remains the first enforcement layer; architecture tests provide clearer policy failures and cover rules that references alone cannot express.

## 6. Application flow and module interfaces

### Request path — **Decided**

```text
HTTP adapter grouped by owning module in the API host
  -> authenticated actor + validated request
  -> capability-shaped module Contract
  -> vertical-slice command/query implementation
  -> aggregate/domain policy and owned persistence
  -> direct call to another module's small contract only when required
  -> commit local transaction
  -> result mapped to HTTP response by the same slice
```

Do not add a generic mediator, repository, unit-of-work, result, or event-bus abstraction in phase 1. Introduce a seam only when there are two real adapters or when the seam hides meaningful policy. This avoids replacing business code with a shallow internal framework.

Use host-owned ASP.NET Core Minimal API route groups organized by product module under `apps/Api/Modules/{Module}`. An endpoint invokes a capability-shaped module Contract; the implementation reaches the same vertical-slice application use case used by trusted in-process callers, without routing through HTTP. Module implementations do not reference ASP.NET Core. Do not add MediatR or a home-grown mediator/pipeline. Cross-cutting decorators or filters require demonstrated repeated behavior.

Do not introduce `IRepository<T>`. Aggregate-specific internal repositories are allowed when they hide meaningful persistence semantics—for example, Stock Position hydration/append and potentially Sales Order aggregate persistence. Simple module-local queries and technical tables may use the module DbContext or a focused query service directly. Repository interfaces never appear in Contracts.

Contract interfaces should be capability-shaped rather than handler-shaped. For example, prefer one cohesive `IReservationCapability` over exposing every internal command handler. Exact names must come from the product language.

A CLR-public contract is not automatically a public HTTP API. Each capability and command is classified as user-facing, module-to-module, workflow-only, or administrative. The API maps only explicitly user-facing endpoints. Workflow-only operations receive a trusted application-created execution context, remain subject to tenant and business-invariant validation, and are audited without accepting a caller-selected system identity from HTTP input.

### Error model — **Decided**

- Contracts return a small discriminated result for expected business outcomes.
- Unexpected faults throw and are handled at the process edge.
- HTTP Problem Details is an endpoint concern, not a Contracts dependency.
- Stable machine-readable error codes are defined only for outcomes callers actually branch on.
- Use operation-specific result types rather than a generic `Result<T>` or third-party union dependency.

## 7. PostgreSQL, migrations, and transactions

### Local transactions — **Decided**

- Each command uses one module DbContext and a short PostgreSQL transaction. Prefer one final `SaveChangesAsync` when the use case can naturally stage all changes before committing.
- One `SaveChangesAsync` call is transactional, but multiple calls are separate commits unless the use case explicitly starts and owns a transaction. Reusing the same DbContext does not make multiple saves atomic.
- A slice that genuinely needs intermediate saves wraps them in an explicit `BeginTransactionAsync`/commit boundary. Do not hide this behind an ambient `TransactionScope`, generic Unit of Work, or repository wrapper.
- Each module sets its own default schema and its own migrations-history table inside that schema.
- Migrations are generated, reviewed, and applied per module.
- Application roles should not rely on `search_path`; mappings use explicit schema names.
- Production migrations run as a pre-deployment operation/job rather than opportunistically from every app replica.
- A finite Migrator project references module implementations and invokes their module-owned migrations in a declared order. It is packaged at the same source/image version as the application, acquires a PostgreSQL advisory lock, exits nonzero on failure, and runs as a separate pre-deployment resource rather than an application startup behavior.

Lifecycle and retention are model-specific. Do not add a universal soft-delete interface,
interceptor, or query filter. A state-stored aggregate uses an explicit lifecycle state when the
ended record remains meaningful history, and physical deletion only for technical, replaceable, or
retention-expired data whose owning policy permits it. Queries deliberately choose whether they show
current or historical records; mandatory tenant isolation remains a separate global filter.

### Cross-module atomic transaction spike — **Deferred; gated**

The first Order Fulfilment workflow uses separate commits and does not justify this spike. Only promote it after a later named short workflow demonstrates a real all-or-nothing invariant that cannot tolerate durable coordination.

The spike will:

1. Open one `NpgsqlConnection` and one `NpgsqlTransaction` in a scoped infrastructure coordinator.
2. Ensure each participating EF Core DbContext uses that same connection and enlists in that transaction.
3. Keep the transaction object out of public module contracts.
4. Resolve participating module capabilities only after the atomic scope has started.
5. Commit only after all calls succeed; roll back on exception, cancellation, concurrency conflict, or injected database failure.
6. Record one trace/correlation ID across the operation.

Required integration tests:

- both module writes commit on success;
- first-module write rolls back when the second module fails;
- outgoing messages/audit entries created by either participant also roll back;
- a cancellation or optimistic-concurrency failure rolls back everything;
- retry behavior does not duplicate changes;
- no context accidentally opens a second connection;
- normal non-atomic calls still use independent local transactions.

**Risk:** EF Core execution strategies, DbContext construction order, pooled contexts, and connection ownership can invalidate a seemingly correct shared transaction. The spike must use production registrations and a real PostgreSQL container.

**Decision rule:** adopt shared transactions only for named workflows whose invariant cannot tolerate eventual consistency. Do not build a universal distributed unit of work.

### Separate-commit workflows — **Decided**

Model each as a persisted process state machine owned by the business capability that owns the outcome. The first is the Sales-owned Order Fulfilment Process; do not create a generic orchestrator project. A normal Rebus handler adapts incoming messages to application code, while the Sales EF Core context atomically persists the inbox receipt, concrete process state, owned domain changes, deadlines, audit/activity, and outgoing outbox rows. Rebus `Saga<TSagaData>` remains a possible focused spike rather than the baseline because its persistence must not obscure that transaction boundary.

Store current state, stable business-operation IDs, message IDs, attempt counts, deadlines, and compensation state. Idempotency exists at delivery, process initiation, transition, business-operation, outbox, and external-effect boundaries; transport message deduplication alone is insufficient. The first compensation is an idempotent Reservation Release requested by the Order Fulfilment Process.

Workflow-only commands are contract-visible without becoming HTTP endpoints. They receive an application-created system execution context and still validate tenant, process correlation, ownership, state, and idempotency. No caller may acquire system identity through request data. External calls use explicit idempotency keys when the provider supports them.

The first process requests Inventory reservation independently per Sales Order line and Stock Position stream. It accumulates reserved and shortage outcomes rather than introducing an atomic multi-stream reservation abstraction. A later multi-stream invariant must justify its own event-store transaction design.

### Row-level security — **Deferred to the adopting product**

The reference implementation enforces discriminator tenancy through required tenant keys, tenant-scoped indexes, EF query filters, write validation, module contracts, and cross-tenant integration tests. It does not configure PostgreSQL row-level security. An adopting product may add RLS when its threat model justifies the connection, transaction, migration-role, administration, and background-worker complexity; the template documentation will identify the extension point and required proof tests.

## 8. Event sourcing, projections, and audit

### Gate G1 — Event-sourcing decision — **Decided for the reference proof**

Begin with one Inventory aggregate family: a Stock Position identified by organization, stocking location, and SKU. This is a deliberate architecture-capability proof rather than a claim that every ERP stock model requires event sourcing. It must demonstrate optimistic concurrency, deterministic hydration, inline projection atomicity, recorded-time reconstruction, immutable correction history, and event-schema evolution. Sales, Access, Purchasing, and other Inventory models remain state-stored during the first workflow. A second concrete event-sourced aggregate, preferably in another module, is explicitly scheduled in Increment 8.1 before library extraction; its domain and owning charter are selected then.

V1 reconstructs what the system had recorded at an event-store position or UTC recorded timestamp. Global position orders equal timestamps. Effective-time/bitemporal history, retroactively effective events, and rewriting past business truth are deferred.

Event sourcing remains an internal Inventory persistence choice and does not leak event-store types or raw events through module contracts.

A Stock Position has an opaque internal stream ID and is uniquely selected by organization, Stocking Location, and Stock Item. Human-facing routes use the organization slug plus location and SKU codes rather than exposing the stream ID. A Reservation is an entity within this stream, not another aggregate: the Stock Position therefore enforces availability across all of its reservations atomically.

Inventory quantities use a constrained decimal `Quantity` in the Stock Item's immutable base unit. V1 supports fractional base units but no conversions, packaging units, or multi-unit arithmetic. The domain enforces `on_hand >= 0`, `reserved >= 0`, `reserved <= on_hand`, and derives `available = on_hand - reserved`; an adjustment below the reserved quantity is rejected until reservations are explicitly released. Negative inventory, overselling, reservation priority, and forced reconciliation are deferred.

Reference-domain defaults exist only to make the architecture proofs coherent: a Stock Position is explicitly opened at zero quantity; SKU and location codes are immutable in normal v1 workflows; and each Sales Order line reservation is all-or-nothing while different lines may produce different outcomes. These are not template abstractions or prescriptions for adopting products, and further ERP policy detail is left to implementation judgment unless it changes an architectural boundary.

### Minimum self-built event-store design — **Decided, constrained to Inventory**

Implement the event store through EF Core in `InventoryDbContext` so stream updates, event inserts, and inline projections share one native transaction. Use EF optimistic concurrency for the stream version. Localized Npgsql/SQL remains available only if a measured or correctness-critical append operation cannot be expressed safely; do not introduce Dapper for this proof.

Within the owning module schema:

- `streams`: domain-neutral tenant, stream ID/type, current version, and timestamps; Stock Item/Location fields do not belong on the technical header;
- `events`: globally ordered position, event ID, stream ID/version, stable event name, schema version, recorded time, JSON payload, and metadata;
- unique constraints on event ID and `(stream_id, stream_version)`;
- append with optimistic expected-version checking;
- immutable event records; ordinary corrections append explicit correction events and never update or delete prior events;
- aggregate removal or retirement appends a lifecycle event and retains the stream; view-specific projections may omit the ended aggregate, but a required write model used for identity lookup retains its retirement identity. Privacy erasure or destructive sanitization remains a separately governed retention process;
- event serializer registry with explicit persisted names, never raw CLR assembly-qualified names;
- metadata for correlation, causation, actor, tenant (if applicable), and trace context;
- payload classification so secrets and unnecessary personal data are never stored;
- no separate periodic hydration-checkpoint snapshots or snapshot abstraction until measured stream length or hydration time breaches an agreed target; an aggregate-shaped inline write model is allowed and recommended by default.

The Stock Position uses one evolution entry point for both paths:

```text
Decide(current state, command) -> new events
Evolve(current state, event)   -> new state
```

The aggregate wrapper owns stream identity, expected version, current state, and uncommitted events. Historical and newly decided events both pass through `Evolve`; only newly decided events enter the uncommitted collection. Replayed events are never redispatched.

Do not discover or recursively dispatch domain events from EF Core `SaveChanges` or `ChangeTracker`. A same-module use case that atomically changes multiple aggregates coordinates them explicitly in its application handler. Separate-commit reactions belong to a durable process. Persistence-time consumers are limited to deterministic inline projections and explicit audit/outbox mapping; they do not initiate hidden aggregate cascades.

Inline projections owned by that module update in the same PostgreSQL transaction as the append. Projection handlers must be deterministic. A rebuild creates shadow projection tables/checkpoints and swaps only after validation; it must not mutate the event log.

The recommended load-for-writing path uses a complete aggregate-shaped inline write model and captures/verifies the stream version. Live reconstruction uses the same pure write-state evolution for history, verification, and rebuild. The aggregate does not retain original state for persistence staging. Multiple inline views are allowed when actual needs justify them; each owns its state and may ignore known irrelevant events. Projection shapes need not mirror the aggregate. Ordinary reads return committed data; pending-event preview is explicit, and required inline updates commit with the append. Missing/lagging required models need repair before affected writes; concurrent advancement is a version conflict. Stock Position business-key lookup through its required write model is accepted: lifecycle retirement retains identity, rebuilds preserve lookup availability or disable affected writes, and out-of-band deletion followed by an expected-version-zero creation is not independently prevented. JSONB write-model storage remains a proposal awaiting mapping/index proof; business fields never belong on the generic stream header. The [consolidated event-sourcing direction](event-sourcing.md) records utility seams, upcasting, deferred async workers, and late extraction proofs.

Expose recorded-time state and a curated business history through Inventory queries/endpoints. Never expose raw event JSON, CLR type names, schema machinery, or unrestricted metadata as the product timeline.

Use stable persisted event names and explicit version-specific readers. V1 includes one credible older-version fixture that hydrates under the current code, but no generic upcaster framework. Event payloads and metadata use opaque product identifiers and exclude secrets, email addresses, and unnecessary mutable display data.

Integration messages are explicitly published by application code; no automatic domain-event mapper is planned. A future publisher stages outgoing messages in the owning transaction's outbox, not directly on the broker. Async projections are wanted later, initially with a single native background worker and no leader-election framework; competing replicas require ordering, durable progress/claiming, and idempotency proofs before support is claimed.

Required tests include concurrent expected-version failure, atomic append plus inline projection, explicit compatibility fixtures for every persisted event version, deterministic replay, recorded-time reconstruction, failed projection rollback, correction-event history, curated-history mapping, and rebuild equivalence.

### Audit model — **Decided baseline; advanced capabilities deferred**

Do not equate event sourcing with a complete audit system:

- Domain events explain accepted business state transitions.
- Security audit records also cover denied attempts, authentication/authorization changes, administrative access, exports, and operational actions that may not change an aggregate.
- Integration events communicate committed facts and may omit sensitive audit detail.

Each module writes its audit record in the same local transaction as the accepted business change. An event-sourced module may derive the accepted-change audit record inline from the append metadata, but still needs explicit records for attempts/outcomes not represented by domain events. A later asynchronous audit projection may centralize search without becoming the source of truth.

V1 implements module-local accepted-change audit, security-significant denied state changes, membership/role changes, and one curated Sales Order activity timeline. Centralized search/export, tamper-evident chaining, redaction workflows, and generalized timelines wait for defined compliance or operator requirements.

The audit envelope should contain timestamp, actor identity, action, target, outcome, reason code, correlation/trace IDs, source module, and a deliberately minimized/redacted detail payload. Retention, tamper evidence, access control, subject-data handling, and export requirements remain open until product/compliance discovery.

**Risk:** self-built event storage, schema evolution, projection rebuilds, privacy, and audit semantics are each substantial. Doing all of them across all modules on day one would dominate product work.

## 9. Messaging and reliability

### When to introduce messaging — **Decided**

Use direct in-process contracts for immediate queries and commands. The Sales-owned Order Fulfilment Process is the first named separate-commit workflow and introduces Rebus through RabbitMQ, because its Inventory and Purchasing steps must survive restart, redelivery, and delayed completion.

### Durable messaging shape — **Decided, with mandatory failure proofs**

- Use RabbitMQ as the default local broker so acknowledgement, redelivery, poison-message, and dead-letter behavior are exercised against a real broker. Run a smaller compatibility suite against a temporary Azure Service Bus Standard namespace before an Azure release.
- A module persists outgoing integration-message envelopes to its own outbox in the same transaction as its business change.
- A hosted dispatcher sends or publishes committed outbox rows according to their message kind and records attempts/dispatch time. At-least-once delivery is assumed.
- Each consumer stores an inbox/deduplication record in its own schema in the same transaction as its effects.
- Integration messages have a stable logical name, schema version, message ID, correlation/causation IDs, creation time, and producer module.
- Rebus owns serialization, routing, broker settlement, a small bounded technical retry policy, and error-queue forwarding. Expected business retries are explicit process transitions and durable deadlines rather than long-running broker retries.
- The first release does not build distributed technical-attempt counting. Rebus/broker safeguards and error-queue alerting bound technical failures; business attempt counts are durable process state. Revisit before multi-replica public production if failure tests show the process-local Rebus tracker is inadequate.
- Ordering is guaranteed only where the business requires it, normally per aggregate/process key.
- Keep compact completed-process/business-operation tombstones for the lifetime of the associated business record. Keep inbox rows for at least broker retention plus the operator replay window, with 90 days as the example configuration rather than a universal compliance rule. Dispatched outbox payloads may be purged after a shorter operational inspection window.

### Message taxonomy and endpoint topology — **Decided**

- A **domain event** is an internal domain-model fact. It is not automatically a broker contract.
- An **integration command** is durable directed intent with exactly one owning consumer. The outbox dispatcher uses Rebus `Send` to the owning module endpoint.
- An **integration event** is a committed fact exposed across a module boundary. The dispatcher uses Rebus `Publish`, and every subscribing module receives its own broker copy.
- An immediate command or query through a module Contract remains an in-process call and is not an integration message.
- Avoid **external event** as a canonical category because it conflates cross-module messages with third-party/public contracts. A future public or partner event is an integration contract at the application boundary and may require its own adapter and versioning policy.

Use one stable input queue and one isolated Rebus bus for each module that has a real asynchronous consumer. That module queue carries both commands addressed to the module and copies of integration events to which it subscribes. Give it a module-specific error queue, worker limits, prefetch configuration, inbox, and handler container. Inbox and outbox tables are database reliability mechanisms and do not imply separate broker queues.

The complete logical topology is:

```text
producer module transaction
  -> producer's PostgreSQL outbox
  -> outbox relay / Rebus
       command -> target module input queue
       event   -> broker fan-out topic/exchange
                    -> subscriber binding/subscription -> module A input queue
                    -> subscriber binding/subscription -> module B input queue
```

The fan-out topic/exchange is therefore separate broker routing infrastructure, not another module-owned output queue. RabbitMQ represents publish/subscribe with a topic exchange and queue bindings. Azure Service Bus represents it with an event topic plus a subscription for each consuming module endpoint, auto-forwarded by the Rebus transport into that module's input queue. The producer owns only its transactional outbox record and publication responsibility; it does not own subscriber queues.

Do not split command and event queues in v1. Add a separate endpoint only when measured throughput, latency, scaling, poison-message isolation, or a materially different operational policy requires it. Splitting by message kind pre-emptively doubles endpoint registration, workers, error handling, monitoring, and cutover work without improving delivery semantics.

Rebus module endpoints use independent `AddRebusService` providers rather than several keyed buses sharing the application provider: keyed buses do not isolate compatible handler resolution. Stable module queue names provide a useful extraction seam, but extraction still requires redesigning synchronous dependencies and bootstrapping/reconciling a new consumer's current state.

**Decision:** use Rebus core 8.9.4 with its RabbitMQ transport locally and Azure Service Bus transport in Azure, subject to the mandatory failure/compatibility proofs. Do not use Rebus saga or PostgreSQL outbox persistence merely because Rebus is selected for transport. Rebus.PostgreSql 9.1.1 has an unresolved 2026 report concerning outbox transaction ordering/current .NET compatibility, so the baseline is a narrow application-owned EF outbox and inbox. Do not adopt the archived third-party `Rebus.Outbox` package as a workaround.

**Risk:** an outbox plus Rebus transport creates two durable queues. The design must assign ownership and deletion/recovery semantics clearly and avoid redundant layers.

Build inbox/outbox mechanics concretely with the first real producer and consumer, then extract one focused EF/Rebus infrastructure library when a second module demonstrates the repetition. Each module retains ownership of its tables, schema, migrations, and business transaction. Shared mechanics may cover envelope serialization, leasing, dispatch, and EF mapping; business process state, routing policy, and a generic event-bus abstraction remain out. Do not create a separate abstractions package without a second real adapter.

## 10. Authentication and authorization

### Identity and browser session — **Decided**

- Run Keycloak under Aspire as the local OIDC/OAuth2 identity provider; use an Entra External ID external tenant for the eventual SaaS deployment.
- The browser authenticates through a BFF using secure cookies and a server-side ticket store. The BFF, not browser code, holds tokens.
- Identity providers establish an external identity identified by immutable issuer and subject. They do not own product organizations, memberships, permissions, approval limits, or business authorization.
- Keep Keycloak realm/client configuration reproducible and versioned, with development secrets outside source control.
- Modules receive a small application actor context and never Keycloak or Entra SDK types.
- Endpoints enforce coarse-grained access; modules enforce current membership, tenant ownership, business permissions, and invariants.
- The first slice distinguishes only authenticated human execution and trusted Order Fulfilment Process execution. Privileged support impersonation/access is deferred until a real support workflow exists; documenting its future audit requirement does not justify a `SupportActor` abstraction in v1.

### Product authorization — **Decided**

- The Access module owns organization membership and role assignments. V1 system roles and their permission bundles are a reviewed code catalog with stable textual identifiers; assignments persist those identifiers. There are no tenant-editable role definitions or startup-time role-row synchronization in v1.
- Owning business modules define stable permission tokens and their meaning. The product-specific system-role catalog composes those permissions without moving their semantics into the API.
- Organization Administrator is a non-removable system-role definition for access administration, not a universal business superuser. Its assignments are removable except when removal or suspension would leave an active organization without an active administrator.
- Start with ordinary role-to-permission grants. Do not add role inheritance, explicit denies, per-object grants, a policy DSL, or field-level permissions without a demonstrated scenario.
- Application handlers are the authoritative enforcement point because the same use case may be invoked through HTTP, an in-process contract, or a durable workflow. ASP.NET Core policies are HTTP-boundary adapters and fast coarse checks, not the sole enforcement layer.
- Owning modules keep resource and business policy local: order state, separation of duties, approval amount/currency, reservation ownership, and aggregate invariants do not move into Access.
- Workflow-only capabilities use the trusted WorkflowActor and explicit business validation rather than impersonating a human or acquiring a universal system bypass.
- Do not cache roles or permissions in the authentication ticket. Resolve current membership/grants for the request and add distributed authorization caching only after measured need and an explicit revocation policy.

OpenFGA is **Deferred**. Revisit it only when the product has an actual relationship graph such as per-object sharing, nested groups, deep permission inheritance, delegated partner relationships, or reverse “which objects/users are reachable?” queries. If adopted, keep amount/state/invariant checks in their modules and introduce one graph-policy family through snapshot, tail, reconciliation, shadow checks, and a pinned model version. Do not add an `IAuthorizationProvider` switch or tuple-shaped application API in v1.

### Organization and identity lifecycle — **Decided baseline**

- For v1 capability coverage, an authenticated user may create an organization with the minimum profile required by the reference workflow and becomes its first Organization Administrator. This is not a permanent entitlement contract: organization-creation eligibility is an application policy before creation, not an invariant that every User may always create one. Gating creation and introducing a limited Trial lifecycle are deferred commercial-onboarding policies, not v1 domain states.
- Access owns pending invitations, acceptance, active/suspended/removed membership states, system-role assignments, and their audit records. Every active organization retains at least one active Organization Administrator.
- An Organization Administrator may invite an email address that does not yet identify a product User and select system roles. The product creates a single-use, expiring invitation and sends its own link; organization membership is never an identity-provider group or role.
- Do not provision ordinary users through Keycloak Admin REST or Microsoft Graph. The recipient signs up or signs in through the configured IdP. On the validated callback, Access resolves or JIT-creates the product User and External Identity by immutable `(issuer, subject)`, then atomically consumes the still-valid invitation and creates the Membership.
- One application flow supports two deployment profiles. In `OpenRegistration`, the IdP permits the recipient to self-register. In `DirectoryGated`, directory administrators must provision, federate, or assign the person before OIDC succeeds; a rejection leaves the product invitation pending. These are provider admission policies, not product membership rules.
- Permit a successfully authenticated but unaffiliated user to JIT-create its product User and then create an organization or accept an invitation. One User may hold memberships in several organizations without mirroring those organizations into the IdP.
- Inviting an email that already belongs to an active membership is rejected; administrators use explicit role management instead. Replaying an already accepted token is idempotent only for the accepting identity.
- Require a normalized verified provider email to match the invitation address. OIDC does not require `email` or `email_verified`; a provider profile that cannot meet the configured assurance contract fails closed for invitation acceptance. Add application-owned re-verification or another explicitly audited policy only when a real deployment requires it. Email remains mutable contact/binding data, never the durable identity key.
- Keep the invitation bearer token out of OIDC `state`, logs, analytics, and referrers. Persist only its digest, rotate it on resend, and resume authentication through a random short-lived pending-acceptance handle. Framework middleware continues to own OIDC state, nonce, PKCE, and correlation.
- Calling an IdP administration API is deferred until a named environment forbids self-registration or requires migration/SCIM-style provisioning. It then becomes a provider-specific adapter with privileged credentials, reconciliation, and partial-failure handling rather than part of the portable invitation contract.
- The same human authenticating under a different `(issuer, subject)` is a distinct product User in v1. Automatic email linking is forbidden; deliberate account migration/linking waits for a named provider-switch or recovery scenario.
- Access writes invitation, audit, and email-outbox state in one transaction. A native hosted worker sends through the configured email adapter; duplicate delivery after an ambiguous external send is tolerated because invitation acceptance is single-use and idempotent. Do not introduce a general notification framework or Rebus email pipeline for this one use case.
- Machine-to-machine clients, multi-identity account linking, support impersonation, custom roles, and privileged-access management remain deferred until an actual scenario requires them.

### Tenant routes and public identifiers — **Decided**

- Put the organization in canonical bookmarkable routes, initially `/o/{organizationSlug}/...`. Resolve the slug, re-check current membership and permission, and only then construct the immutable request-scoped tenant context. The slug selects an organization; it never authorizes access.
- Do not keep an authoritative active or last-used organization in the BFF session. The root route redirects directly when the user currently belongs to exactly one organization and otherwise shows the organization chooser.
- Let the customer propose its organization slug during onboarding. Normalize, validate, reserve, and make it globally unique. Treat it as immutable in normal product workflows; an exceptional audited support rename preserves the old slug as an alias/redirect.
- Use stable, normalized, tenant-scoped slugs for other genuinely named resources. Use short, tenant-scoped business references for transactional documents, such as a sales-order number; gaps are allowed. Do not add slugs to every technical row or expose long implementation identifiers merely because they are database keys.
- Treat database identity, cross-module identity, and public URL reference as separate concerns when the product benefits from that separation. Authorization never depends on an identifier being difficult to guess.
- Represent enum-like values in URLs, JSON, messages, events, and relational persistence with explicit stable textual tokens rather than CLR numeric ordinals. A C# enum may remain an internal convenience, but renaming its member must not silently rename a persisted or wire value. The first implementation uses a visible, exhaustive per-type value map shared by EF and serialization; introduce a generic attribute convention only after repeated mappings justify it.

Do not try to hide tenant identity from domain/application code. Tenant-owned records receive an explicit `OrganizationId`; reads use tenant-scoped queries/global filters, and a SaveChanges interceptor may inspect changed rows only to reject missing or mismatched tenant IDs. It must not infer/populate tenant IDs, dispatch domain events, or trigger behavior. Background workers establish an explicit tenant scope before resolving a module DbContext.

Authentication integration tests should use locally minted test tokens for most cases, plus a small end-to-end suite against Keycloak to prove issuer, audience, key discovery/rotation assumptions, and browser login/logout.

## 11. Local infrastructure and observability

### Aspire topology — **Decided, introduced by phase**

The AppHost will orchestrate:

- the API application;
- one PostgreSQL server/database;
- Keycloak;
- Mailpit;
- RabbitMQ when the durable fulfilment slice begins;
- Redis for BFF tickets and explicitly justified cache entries;
- Azurite Blob only when an accepted attachment slice exists;
- the minimal Vite frontend only after an HTTP workflow exists.

Aspire is the local-development control plane, not a production runtime dependency or a reason to decompose the application. Aspire packages are limited to the AppHost and topology tests. Runtime projects consume standard .NET configuration and vendor clients such as Npgsql; the AppHost supplies the same standard connection-string/configuration keys that a deployed environment supplies. Use the repository-owned Service Defaults project for OpenTelemetry, health checks, discovery/configuration, and consistent telemetry. Use `aspire start`, `aspire wait`, and resource-aware diagnostics for the AppHost rather than ad hoc process startup.

Use a conventional C# AppHost project, not TypeScript and not a single-file AppHost, so `DistributedApplicationTestingBuilder` can reference and exercise the full topology. Register the finite Migrator as a project resource that waits for PostgreSQL; the application resource uses `.WaitForCompletion(migrator)` so a failed migration prevents application startup locally and in topology tests.

### Cache policy — **Decided baseline**

Redis is selected for BFF ticket storage. V1 implements no product-data or distributed authorization cache. Promote a cache only after measurement identifies a real read path and its source of truth, tenant-aware key, invalidation/TTL, maximum size, stampede behavior, and acceptable staleness.

Implement one focused Redis-backed ASP.NET Core `ITicketStore` because the framework defines the contract but does not provide a Redis implementation. Use versioned, environment/application-namespaced keys, ticket expiry/renewal, and explicit revocation. Redis unavailability fails authentication closed; loss of ticket data signs users out and never falls back to client-side tickets or product persistence. Data Protection key storage remains separate.

### Observability — **Decided baseline**

- Use native `ILogger` structured logging and OpenTelemetry/OTLP as the application-facing observability surface; do not add Serilog or a vendor SDK to the baseline.
- Emit structured JSON logs with trace, correlation, actor (non-sensitive ID), module, and workflow identifiers.
- OpenTelemetry traces across HTTP, module calls, database access, outbox dispatch, message handling, and external calls.
- Metrics for request latency/errors, database pool/transaction health, outbox age/depth, retries/dead letters, inbox duplicates, projection lag/rebuild, and workflow state age.
- Liveness reports process viability only; readiness reports ability to serve without making every optional dependency fatal.
- Never record tokens, secrets, full event payloads, or unclassified personal data in telemetry.
- Aspire's dashboard is the local experience. The production backend remains deployment configuration: initially Azure Application Insights or a self-hosted VictoriaMetrics/Grafana stack, chosen after operational and cost evidence without changing application instrumentation.

## 12. Testing strategy

### Test layers — **Decided**

1. **Domain tests:** pure invariants and state transitions; no DI or database.
2. **Vertical-slice tests:** handler/interface behavior with real module registrations and PostgreSQL where persistence matters.
3. **Module contract tests:** exercise the same public module interface callers use, including expected errors and authorization.
4. **Architecture tests:** enforce the rules in section 5.
5. **Persistence/migration tests:** migrate an empty database per module, verify schema isolation/history tables, and exercise optimistic concurrency.
6. **Consistency tests:** shared-transaction rollback, outbox atomicity, inbox idempotency, process retries/compensation, and event/projection atomicity.
7. **API integration tests:** HTTP routing, Problem Details, auth policies, health, and composition.
8. **Browser smoke tests:** only the critical workflow through the minimal frontend and real local identity provider.
9. **Deployment smoke tests:** migrations, startup/readiness, one business transaction, telemetry, and rollback procedure in the target environment.

Use a real PostgreSQL container for all semantics that SQLite/in-memory substitutes cannot reproduce. Prefer one controlled database fixture with isolated databases/schemas over a container per test. Parallel tests must not share mutable schema state accidentally.

Every automated test must have a documented CI lane and repository command. Focused Testcontainers suites run on ordinary pull requests using a Linux runner with a supported container runtime. A smaller AppHost suite uses `DistributedApplicationTestingBuilder` to prove Migrator completion, topology/configuration, health, and the critical integration path. Browser/Keycloak, RabbitMQ failure, Azure Service Bus compatibility, and deployment tests may run in slower, scheduled, or protected-environment lanes, but must remain reproducible CI jobs rather than developer-machine-only procedures. Tests must not depend on personal credentials or undeclared local state.

Use GitHub Actions on Ubuntu runners as the initial CI implementation. Pull requests run the fast and focused gates; merge-queue, scheduled, or protected-environment workflows run expensive topology, browser, broker-failure, real-Azure, deployment, restore, and rollback suites. Different frequency is permitted, but no automated test may exist without a CI path. Keep test code compatible with the local Podman environment and CI's Docker-compatible runtime rather than relying on runtime-specific hostnames or sockets.

### Quality gates — **Decided baseline**

- Commit a root `.editorconfig`, enable nullable reference types and SDK analyzers, treat warnings as errors, and verify deterministic formatting with `dotnet format` in CI.
- Unit/architecture tests on every change.
- PostgreSQL integration tests in CI.
- Auth/browser tests in a slower CI lane.
- Migration and deployed smoke tests before promotion.
- Persisted event/message fixtures become compatibility tests; deleting or renaming a CLR type cannot silently orphan stored data.
- When the JavaScript workspace exists, use Prettier, commitlint, and Lefthook; hooks improve local feedback while CI remains authoritative. Defer CSharpier unless `dotnet format` proves insufficient.

When frontend work begins, use Vite with a directly pinned pnpm 12 release and commit `pnpm-lock.yaml`. Declare the pnpm version in `package.json` and install that exact version in CI; Corepack may be a developer convenience but is not a repository or build prerequisite. The frontend framework remains deferred until the minimal frontend slice. The C# AppHost does not require npm or another JavaScript host.

## 13. Deployment path

### Gate G3 — First deployment target — **Decided for private pilot**

The first Azure deployment is a deliberately non-HA, restricted private pilot on Azure Container Apps Consumption with PostgreSQL Flexible Server, Service Bus Standard, Azure Managed Redis, ACR Basic, Key Vault, Entra External ID, and bounded Azure Monitor/Application Insights. It uses the cheapest feasible SKUs verified at deployment time, explicit replica/worker/telemetry caps, backup/restore and rollback drills, and no claim of public production readiness. Public gateway/WAF/private-origin topology, HA/SLO/RPO/RTO promotion, AKS, and GitOps remain later gates.

### Initial deployable unit — **Decided**

- One non-root OCI image for the application process.
- One separately runnable migration artifact/job that applies every module's reviewed migrations in a declared order and fails before application rollout on error.
- External PostgreSQL with backup/PITR appropriate to the product.
- Identity, email delivery, cache, and telemetry endpoints supplied by environment configuration.
- Health/readiness endpoints and graceful shutdown for HTTP requests, workers, and message leases.
- Image version tied to source revision; generate dependency inventory/SBOM in CI.

### Kubernetes and Flux — **Deferred**

Do not write manifests yet. If a post-pilot decision selects Kubernetes and the team will operate it, first deploy the image manually to a disposable namespace, document probes/resources/migrations/rollback, then encode those proven operations as Helm/Kustomize resources and add Flux reconciliation. GitOps must represent a working deployment, not serve as the experiment that discovers it.

## 14. Phased delivery and acceptance gates

### Phase 0 — Discovery and decisions

- Capture [prior-attempt failures and explicit countermeasures](./earlier-attempts.md).
- Complete G0 product definition and [module charters](../modules/README.md).
- Review dependency versions/licenses and pinning policy.
- Finish the narrowed Inventory event-sourcing design, organization lifecycle, first workflow invariants, and first Azure deployment target enough to avoid dead ends.
- Record accepted decisions as short ADRs; retain this plan as the roadmap.
- Turn the roadmap into the review-sized [v1 delivery slices](./v1-slices.md).

**Exit:** the repository owner approves the complete Phase 0 documentation change set. Only then scaffold Increment 1.1.

### Phase 1 — Walking skeleton

- Deliver Slice 1: build policy, project graph, architecture tests, CI lanes, API, AppHost, ServiceDefaults, PostgreSQL, and the finite Migrator.
- Establish schema/migration isolation, health, telemetry, and repeatable local lifecycle without inventing business data.

**Exit:** a clean checkout builds, architecture rules are executable, and `aspire start` reaches healthy state only after successful isolated module migrations.

### Phase 2 — Identity and Organization access

- Deliver Slice 2: Keycloak BFF login, Redis tickets, JIT User link, Organization bootstrap/routing, invitations through Mailpit, memberships, and system roles.
- Prove tenant context, access revocation, last-administrator protection, and security-significant audit.

**Exit:** two real users can enter one Organization with distinct business roles, and current membership controls every request.

### Phase 3 — Inventory and Sales domain proof

- Deliver Slices 3 and 4: state-stored reference data, one event-sourced Stock Position family, temporal reads/rebuild, Customer/draft order, and approval policy.
- Prove optimistic append, deterministic hydration, inline projection atomicity, audit separation, one batched in-process Inventory query, and explicit Sales transaction ownership.

**Exit:** the Stock Position proof either confirms ADR 0016 or supersedes/narrows it before durable messaging builds on the model; an approved order has initial fulfilment process state but no broker work yet.

### Phase 4 — Durable fulfilment and extraction seams

- Deliver Slice 5: module-isolated Rebus endpoints, concrete inbox/outbox/process state, reservation outcomes, snapshot-plus-tail bootstrap, Purchasing requirement, cancellation compensation, and operator/failure evidence.
- Extract shared EF/Rebus mechanics only if two implemented consumers demonstrate the same deep module.

**Exit:** the workflow survives process termination, concurrent/duplicate/out-of-order delivery, poison messages, and compensation without corrupting outcomes; a late consumer and endpoint cutover are proven.

### Phase 5 — Minimal frontend journey

- Deliver Slice 6: Vite/BFF Organization shell and only the screens required to exercise the reference workflow.
- Keep most behavioral coverage below the browser and add focused Playwright smoke paths against Keycloak.

**Exit:** a user can sign in, complete the workflow, observe its status, and sign out.

### Phase 6 — First real deployment

- Deliver Slice 7: OCI packaging, environment contract, Azure Container Apps private pilot, managed dependencies, spend/abuse ceilings, compatibility suites, restore, and rollback.
- Do not add Kubernetes/Flux or claim public production readiness.

**Exit:** a clean environment can be deployed from source and remains observable and recoverable.

### Phase 7 — Product extraction

- Deliver Slice 8: first prove a second concrete event-sourced aggregate, preferably in another module, then extract demonstrated technical mechanics while retaining reference business behavior as sample code. Finally execute bounded configuration-driven scaffolding and the reviewed copy/rename or agent-assisted checklist against a concrete second repository; scan durable identifiers, run all tests, and deploy through the same path.

**Exit:** a second repository builds, tests, starts locally, and deploys without changes to a reusable foundry framework.

A naming/configuration script may serve the final handoff. A broad wizard, provider matrix, or extensible generator requires an actual consumer need and exercised alternatives; it is not a prerequisite for the working reference application.

## 15. Technology-selection posture

### Accepted implementation baseline

Versions here are the researched stable baseline as of 2026-09-23, not floating constraints:

| Technology | Baseline version | License | Decision note |
| --- | --- | --- | --- |
| .NET / ASP.NET Core / EF Core | 10.0.12; SDK 10.0.112 | MIT | Adopt .NET 10 LTS and Ubuntu's maintained 1xx SDK feature band; support ends 2028-11-14 and requires current patches |
| Aspire | 13.5.4 | MIT | Adopt for local orchestration/topology tests; only latest feature release is supported |
| PostgreSQL | 18.6 | PostgreSQL License | Adopt current minor; major 18 supported to 2030-11-14 |
| Npgsql / EF provider | 10.0.3 | PostgreSQL License | Adopt with EF Core 10; prove transaction behavior on this combination |
| Keycloak | 26.7.4 | Apache-2.0 | Adopt for reproducible local identity; production uses Entra External ID; only the latest Keycloak minor is supported |
| Mailpit | 1.31.2 | MIT | Local/test only; pin image version/digest |
| Redis | 8.2.10 extended line | AGPLv3 selected from Redis 8's tri-license | Adopt locally for BFF tickets only; product-data caching remains deferred |
| RabbitMQ | 4.3.6 | MPL-2.0 | Adopt for local acknowledgement/redelivery/fan-out failure tests; keep an active upgrade cadence |
| Rebus core / ServiceProvider / RabbitMQ / Azure Service Bus | 8.9.4 / 10.7.2 / 10.1.1 / 10.7.1 | MIT | Adopt only when Slice 5 begins; recheck package compatibility then |
| ArchUnitNET | 0.13.4 | Apache-2.0 | Adopt and pin; active and expressive, but pre-1.0 |
| Testcontainers for .NET | 4.15.0 | MIT | Adopt for focused real-infrastructure integration tests |
| OpenTelemetry .NET | 1.18.0 | Apache-2.0 | Adopt through the repository-owned Service Defaults project |

Use central NuGet package management and exact container tags/digests after plan approval. Pin SDK 10.0.112 with `latestPatch`: it carries the same 10.0.12 runtime as SDK 10.0.401 while remaining discoverable through Ubuntu's supported package channel and the development VM's editor tooling.

### Conditional

- Rebus and its selected transport enter only for the named Order Fulfilment workflow. `Rebus.PostgreSql` outbox and Rebus sagas are excluded from the baseline.
- Redis is used for BFF tickets under the selected AGPLv3 option. Product-data caching remains deferred.
- Minimal frontend only after the backend workflow exists.
- Vite is the frontend build/dev tool and pnpm is the package manager; Corepack is not required. Select the UI framework when the minimal frontend phase begins.
- Kubernetes/Flux only after a post-pilot operational driver selects that operating model.

### Explicitly excluded initially

- MassTransit and commercial dependencies.
- Critter Stack, Brighter/Lighter, or another opinionated application stack.
- A generic mediator/CQRS framework, generic repository, home-grown application framework, or code generator.
- Multiple deployable module processes.

The [companion research note](../research/2026-09-23-technology-baseline.md) is the evidence source for exact versions, licenses, support status, transaction caveats, and primary documentation. Re-check it before implementation if the baseline date is no longer current.

## 16. Major risks and countermeasures

| Risk | Countermeasure / decision trigger |
| --- | --- |
| Modules are invented before the domain is known | G0 module charters and the accepted reference workflow define ownership before scaffolding |
| Contracts become DTO/repository dumping grounds | Deep capability interfaces, internal-by-default implementation, architecture tests |
| API becomes an application layer | Endpoint slices live in modules; API contains composition/policy wiring only |
| Cross-module transaction becomes a global unit of work | Named-use-case gate, one-connection proof, failure injection, no transaction in Contracts |
| Self-built event sourcing consumes the project | One aggregate spike; explicit adoption ADR; state storage remains default |
| Event log is mistaken for security audit | Separate semantics and coverage for denied/operational actions |
| “Reliable” messaging loses or duplicates effects | Transactional outbox, consumer inbox, idempotency, restart/duplicate tests |
| Infrastructure is chosen from a wishlist | Add resources only when a workflow needs them |
| Keycloak becomes an unplanned production platform | Keycloak is local only; the Azure pilot conformance-tests Entra External ID |
| Kubernetes/GitOps obscures application progress | The Container Apps pilot and a named operational driver precede Kubernetes/Flux |
| Template abstractions leak example assumptions | Deploy concrete example first; extract by copy/rename; automate only proven repetition |
| One database weakens isolation | Explicit schemas/mappings, migrations per module, reference and model tests; consider roles only if compatible with chosen transactions |

## 17. Phase 0 closure and implementation gate

Phase 0 has produced the failure/countermeasure record, four module charters, domain glossary, accepted/deferred architecture decisions, primary-source technology evidence, deployment direction, v1 scope, and review-sized delivery plan. No further broad domain or architecture drilling is required before implementation.

Remaining unknowns are deliberately attached to later increments: exact source type/folder names, frontend framework selection, Azure infrastructure-definition mechanism, real-product repository/name, compliance-driven audit retention, and public-production gateway/HA requirements. None justifies speculative v1 code now.

Repository-owner approval of the complete Phase 0 change set is the implementation gate. Once recorded, Increment 1.1 in [V1 delivery slices](./v1-slices.md) is the only authorized implementation scope; later increments remain planned, not implicitly authorized.
