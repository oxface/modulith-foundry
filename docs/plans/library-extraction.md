# Library and sample extraction plan

Status: review proposal, 2026-10-03. The owner approved the archive-and-plan direction.
This document proposes implementation increments; it does not freeze public interfaces,
package boundaries, or authorize a commit. E1 was reviewed and checkpointed as `a8e45c9`;
[the checkpoint report](../reports/e1-tenant-actor.md) records that original combined design.
The independent split was reviewed and checkpointed as `c8cbf64`;
[its current report](../reports/e1-identity-split.md) records fresh proofs.
E2.1 was [reviewed and checkpointed as `50e7933`](../reports/e2-1-tenant-ownership.md).
[E2.2 module-owned migrations](e2-2-module-migrations.md) are implemented for review;
relationships, version conflicts and [the remaining E2 scope](e2-persistence.md) are still planned. Read
[the approved design posture](../design.md) alongside this plan.

## Delivery model

Preserve the old backend and documentation as an executable reference, then deliberately
reimplement one justified mechanism at a time. Introduce its real sample usage immediately;
avoid a library-only phase followed by a large sample integration. The wholesale domain may
be reused, with freshly documented module responsibilities as behavior is added.

An implementation increment delivers the public interface, meaningful mechanism, focused
interface tests, executable sample integration, relevant failure proofs, and documentation.
Add a template recipe or file only when a new consumer setup pattern is exercised. The sample
can supply the concrete source for the final template; it is not a third implementation.
Sample-only capability proofs and adapter increments may introduce no new library.

### Checkpointed actor identity and tenancy

Before implementing E2, the owner authorized revising E1 into independently adoptable
`ModulithFoundry.ActorIdentity` and `ModulithFoundry.Tenancy` libraries, with no dependency
between them. The split was owner-reviewed and checkpointed as `c8cbf64`; see
[the E1 plan](e1-tenant-actor.md).
Actor means the identity performing an operation, not an actor-model execution component.
Executing actor and optional initiator stay together; tenant selection and tenantless
execution belong to the separate tenancy context. The checkpointed combined implementation
is historical evidence at `a8e45c9`. Each holder is single-assignment; combined composition
and completion of required establishment belong to the host.

The tenancy core accepts an explicitly established tenant without prescribing a URL shape.
For E3, propose optional HTTP utilities for configurable route values and hostnames, plus a
consumer resolver for other strategies such as one tenant per application user. Candidate
selection, canonical identity resolution and admission are distinct responsibilities.
Organization remains the sample's domain/UI term; Tenant denotes the technical isolation
boundary. Membership is a separate consumer-owned Access increment in E3, not a prerequisite
or dependency of either foundation library.

### Extraction and strategy gate

The owner confirmed standalone library adoption as the current strategy: a segment can be
used in an ordinary .NET API or worker without the template's module structure or Access
model. This can be revisited if actual implementation shows value in more involved libraries.
Until reviewed otherwise, standalone consumer proofs remain part of the relevant increments.

Access begins as customizable sample/template code. Its presence in the template makes later
extraction possible when reuse earns it; a reusable Access module is not required for E1.
Apply the same evidence-based assessment to other candidate mechanisms rather than treating
the candidate inventory as a promise that each entry becomes a package.

Before promoting template behavior into a library or changing the adoption strategy, present:

1. Concrete consumers or use cases that repeat the behavior, or implementation evidence of
   meaningful complexity removed by the proposed integration.
2. Which behavior is a reusable mechanism and which product policies would travel with it.
3. Consumer usage and customization examples, dependencies and ownership changes, and the
   effect on standalone adoption and existing compositions.
4. Public-interface and end-to-end proof obligations, compatibility responsibilities and
   supported limits. A more involved implementation does not automatically inherit the old
   guarantees or supersede explicit-control decisions.

Review that proposal before promotion. Access extraction may produce an optional feature
module with explicit policies rather than a technical foundation; do not move its membership
or role model into the tenant/actor seam to make extraction convenient. New evidence and a
reviewed strategy change can reorder later increments; neither is silently assumed.

### Planned layout

- `src/`: opt-in runtime libraries and provider-specific adapters.
- `samples/Wholesale/`: API composition root, modules/Contracts, finite Migrator, AppHost,
  host ServiceDefaults, and sample-specific tests, introduced as needed.
- `tests/`: library-interface and independence tests introduced with each mechanism.
- `templates/`: exercised consumer setup, consolidated after working sample usage.
- `archive/proof-sample/`: frozen original source, tests, fixtures, and historical documents.
- `docs/`: current decisions, development commands, reviewed slice designs and proof reports.

Create an active root solution with the first active projects. It must exclude the archive;
the archive retains its own solution, build policy and test lanes. Active references never
point into the archive. Library naming and exact package splits are reviewed within the
first increment that needs them; no empty projects are added to reserve names.

## Candidate inventory

Candidates are proposals supported by archived implementations, not already-proven library
interfaces. Links below deliberately point to the historical evidence.

| Capability | Reusable candidate | Consumer-owned part and decision |
| --- | --- | --- |
| Actor identity and tenancy | Independently adoptable identity and tenant-choice contexts, each with explicit scope validation | Membership, roles, invitation lifecycle, tenant admission, actor trust and authorization remain consumer policy. This is an agreed early library direction. |
| Module persistence | Explicit EF ownership-filter/model utilities and justified write validation | Module DbContext, schema, mappings, migrations and transaction ownership. The archived `shared/Persistence` utility is a starting comparison, not a required base context. |
| Event identity and codec | Explicit alias/version registry, payload encoding/decoding and compatibility errors | Event definitions, required/optional fields, allowed schemas and evolution. Compare both serializers before designing a common interface. |
| Event history | Contiguous ordered-range checks, captured-head reads and deterministic hydration mechanics | Domain reducer, state shape and temporal meaning. Preserve application append time versus commit time. |
| Event append | Stream expected-version checks and event/envelope staging in native EF transactions | Business-key identity, decision state, view definitions, audit and transaction owner. PostgreSQL guarantees stay explicit. |
| Inline projections and repair | Explicit batch coordination and bounded reconstruction helpers where genuinely shared | View identities/reducers, required-view policy, write admission, lock granularity and privileged recovery. Purchasing repair is not yet proven. |
| Reliable messaging | Inbox/outbox storage, lease claims, token-guarded completion/backoff and callable dispatch | Semantic operation identities, fingerprint meaning, producer trust, retention, routes and payload mapping. Delivery deduplication is not business idempotency. |
| Rebus/RabbitMQ integration | Optional adapters around demonstrated delivery/publishing behavior | Consumer creates endpoints and wires queues, topics, subscriptions, handlers, routing and retry/error policy. No generic bus registration facade. |
| Audit | Common technical envelope and explicit staging, if comparison earns a library | Action/reason vocabulary, denial policy, sensitive details, retention and query visibility. Event streams are not security audits. |
| Authentication and Access | Evaluate focused technical utilities and, later, an optional Access feature module if reuse earns it | BFF/OIDC settings, principal completion, users, Organizations, memberships, invitations and roles begin as customizable sample/template code. Promotion follows the extraction and strategy gate; Access is not a technical-library foundation. |
| Saga/process coordination | Compare repeated persistence/claim mechanics after reliable delivery | Concrete transitions, deadlines, compensation and operator attention remain in Sales/Purchasing. No generic saga engine is justified yet. |
| DDD and CQRS | Small pending-event or value utilities only if they remove meaningful duplication | Rich aggregates, children, value-object rules, domain services, handlers/results and direct queries begin as consumer code. No mandatory inheritance or mediator. |
| Structure, API and registration | Explicit sample conventions and configurable architecture utilities | Consumer-selected graph, HTTP ingress and module composition. A configurable policy tool must demonstrate an alternative layout and detect violations. |
| Testing and delivery | CI lanes, fixtures, architecture checks and development tooling | Extract helpers only when used; template pipeline is not a runtime dependency. Dependabot or an alternative enters with a reviewed update policy. |

Evidence entry points:

- [Tenant context and module collaboration](../../archive/proof-sample/docs/modules/README.md).
- [Second aggregate comparison](../../archive/proof-sample/docs/plans/second-event-sourced-aggregate.md).
- [Event-sourcing gate](../../archive/proof-sample/docs/plans/event-sourcing-correctness-gate.md).
- [Messaging reuse comparison](../../archive/proof-sample/docs/plans/messaging-reuse.md).
- [Durability closure](../../archive/proof-sample/docs/plans/durability-closure-checklist.md).

## Dependency and interface review

Actor-identity and tenancy libraries have no dependencies on each other, EF, web, transport,
Aspire or sample Access.
Provider-specific persistence depends on the selected EF/PostgreSQL packages and only the
smaller seams its tested mechanism requires. An event codec need not depend on a database;
event persistence and messaging need not depend on one another. Rebus integration is optional.
Audit and other segments do not acquire unrelated transitive runtime packages for convenience.

Decide whether a separate abstractions project is useful only when actual consumers require
it. Replacing a concrete dependency with a wrapper is insufficient justification. Service Bus
is a future real-adapter proof, not an interface designed solely from RabbitMQ assumptions.
Keep provider-specific capabilities available alongside any later common subset.

Before implementing an increment, present:

1. What complexity the interface removes, backed by concrete archived consumers.
2. A small consumer usage example, required registrations and configuration, and expected errors.
3. Who creates the scope/context/connection, stages changes, saves, commits, retries, publishes
   and disposes; include cancellation and concurrency behavior.
4. Supported options and how each changes the guarantee; distinguish policy replacement from
   ordinary configuration. Schema and route choices cannot carry sample defaults implicitly.
5. Public types, actual dependencies, independent-adoption examples, and test/CI lanes.
6. Archived proof scenarios transferred, known limits retained, and behavior deliberately changed.

The following is an illustrative transaction shape, not a selected public interface:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

// These operations stage into the caller's context and never save or commit.
await events.StageAppendAsync(db, append, cancellationToken);
audit.Stage(db, auditEntry);
outbox.Stage(db, explicitlyMappedMessage); // Only when messaging is opted in.

await db.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

A necessary repair/write guard may be acquired before loading state and held through commit.
A scope helper must document whether it establishes that guard, a transaction, or only context.
Disposal never silently commits. Define or reject nesting, context reuse, and concurrent use.
Unexpected storage faults require a fresh operation scope unless a narrower reuse guarantee is
explicitly demonstrated. This example assumes a single owning-module transaction and makes
no cross-module transaction claim.

## Proposed implementation increments

Every increment leaves a runnable sample or preserves its existing runnable behavior. Each
has library/template/sample findings and the verification described below. Split further if
the interface and failure matrix cannot be reviewed together in one sitting.

### E0 Archive and active documentation

This checkpoint was reviewed and committed as `0690475` on 2026-10-03.

Preserve the original tracked source and its provenance without implementation changes.
Retain minimal active policy, tooling, CI and a proposed extraction plan. Verify checksum
identity, relocated solution/build discovery, formatting, Fast tests and representative
container/host paths. The owner reviews retained/new files and tooling changes rather than
line-by-line relocation. No new reusable mechanism is proven by this increment.

Verification on 2026-10-03: all 800 original tracked files match the snapshot manifest;
the relocated 20-project solution restores and builds with zero warnings. Root/archived
formatting, semantic style, analyzers, active documentation links, YAML and workflow paths
passed. Architecture passed 21 cases, application 27, and the full PostgreSQL lane 163.
Focused broker replica-death checks passed four cases across all three consumer modules;
one Topology case passed two full application lifecycles with migration/health/telemetry
checks. Broker and Topology selections are focused relocation proofs; their complete suites
remain wired into CI. Local restore needed network access for NuGet vulnerability data;
format/test hosts needed local IPC/container access. No audit or assertion was disabled.

### E1 Independent actor identity and tenancy with a minimal sample

The owner reviewed the original combined E1 implementation and authorized checkpoint
`a8e45c9`; [its report](../reports/e1-tenant-actor.md) remains historical evidence. The owner
subsequently authorized the independent split, owner-reviewed and checkpointed as `c8cbf64`; see
[the revised plan](e1-tenant-actor.md) and [current report](../reports/e1-identity-split.md).
New lifecycle/concurrency and independent-adoption proofs are distinct from archive evidence.

Context is immutable within one operation; changing tenant, actor or initiator requires a
separate operation context. Use opaque string keys in distinct tenant/actor value types,
with consumer-owned domain identity mapping. Keys use ordinal comparison, reject empty or
whitespace-only values, and preserve accepted values exactly; canonicalization is consumer
policy. Operations read each selected segment through its injected, read-only accessor. The
host initializes each exactly once per operation scope through its separate initialization
interface, finishing the segments required by a capability before invoking it. Reading
before establishment and any repeated initialization are errors; no reset, replacement or
anonymous fallback is supported. Review explicit registration and lifetime for human and
workflow consumers. Immutable values do not depend on DI. Tenant selection never asserts
membership or permission. System actor identities come from trusted consumer wiring, not
caller input.

Support concurrent reads after initialization within one operation; parallel business work
starts only after establishment completes. Provide small, explicitly called context
requirement checks; their use and failure presentation are consumer choices. Establishing
trust, resolving membership, checking permissions and mapping HTTP failures remain outside
the core seam.

Support explicit tenantless execution separately from missing required context. Keep current
actor and original initiator distinct, including a workflow acting on work initiated by a
human. Initiator is optional, explicitly supplied when known, and neither inferred from the
actor nor automatically propagated. Neither attribution nor context establishment grants
permission; consumer policy owns authorization and the effect of later membership revocation.

Represent anonymous execution explicitly, separately from missing actor context and from
tenantless execution. Consumers choose which capabilities permit anonymous actors. A public
tenant-specific operation and an identified actor's tenantless operation are both valid
compositions when the owning capability permits them.

Provide a runnable sample host and two real capability calls exercising isolated contexts,
with concurrent operations, permitted tenantless work, rejected missing required tenants,
permitted anonymous work, rejected anonymous actors where an identified actor is required,
missing/mismatched context cases and distinct actor/initiator attribution. Test setup actors
are not a production authentication implementation; expose tenant business HTTP operations
only after trusted ingress is in place. Prove exact key comparison and validation, early-read
and repeated-initialization errors, optional attribution, concurrent reads within a scope,
no cross-scope leakage, explicit requirement checks, forbidden scope rebinding and cleanup
on exceptions/cancellation. Build a minimal consumer without EF, ASP.NET Core, Rebus, Aspire
or sample Access.Contracts. Add active solution, Fast lane and hook coverage.

Template output: explicit context establishment and lifetime recipe if this is a new setup
pattern. Domain policy stays in the sample.

Historical comparison: Access uses [GUID-based tenant IDs](../../archive/proof-sample/modules/Access/Access.Contracts/Organizations/OrganizationId.cs)
and [user IDs](../../archive/proof-sample/modules/Access/Access.Contracts/Identity/UserId.cs),
while [external identity](../../archive/proof-sample/modules/Access/Access.Contracts/Identity/ExternalIdentity.cs)
uses issuer/subject strings and [Sales workflow identity](../../archive/proof-sample/modules/Sales/Sales/Fulfilment/OrderFulfilmentProcess.cs)
is a name. This does not prove that a reusable context must prescribe GUIDs. Archived
[HTTP admission](../../archive/proof-sample/apps/Api/Modules/Access/Middleware/OrganizationScopeMiddleware.cs)
and [module authorization](../../archive/proof-sample/modules/Sales/Sales/Authorization/SalesRequestAuthorization.cs)
are explicit consumer checks; they do not prove a universal library authorization mechanism.

The owner agreed to start the sample/template with a stable, globally unique application
UserId, with consumer-owned external-identity resolution feeding the human actor key.
Authentication-provider details stay outside both foundation libraries; neither requires
provider fields nor an application User entity. Human/system actor kind distinguishes
otherwise equal key text.
In the archive, [identity resolution](../../archive/proof-sample/modules/Access/Access/Identity/LinkExternalIdentityHandler.cs)
matches issuer and subject; an unrecognized pair creates a new user. It does not automatically
merge users with matching email addresses. For OIDC, [claim stability rules](https://openid.net/specs/openid-connect-core-1_0.html#ClaimStability)
identify issuer/subject as the stable pair and do not guarantee email stability or uniqueness.
Recommend explicit, verified account linking as consumer Access policy; its implementation
and admission choices belong to a later sample slice, not E1.

### E2 Explicit EF tenant and module persistence utilities

The owner confirmed shared database/module schemas/tenant discriminator, immutable
ownership for ordinary persistence and native development-time migration scaffolding.
Review [the implemented first increment](e2-1-tenant-ownership.md) and
[its fresh proof report](../reports/e2-1-tenant-ownership.md). Explicit EF ownership filters,
tracked-write validation and native ownership predicates passed 61 tests with an Inventory
consumer and an independent GUID consumer. [The complete E2 plan](e2-persistence.md)
and [earlier design findings](../reports/e2-persistence-design.md) distinguish the remaining
scope. Shared cross-module transactions receive a separately
designed workflow/proof rather than being implied by this increment.

Use real sample module DbContexts and two consumer-selected schemas. Review explicit model
registration, tenant-owned rows/indexes and the chosen write-validation mechanism. It must
handle inserts, updates, ownership changes and relevant child relationships through tested
semantics; query filters alone do not prove all write paths are safe. Document raw SQL and
privileged bypass responsibilities. Configure an alternative ownership/schema policy to
prove the utility does not freeze the sample layout.

Prove missing/foreign context, cross-tenant identifiers, model/migration ownership, competing
writes and rollback on PostgreSQL. Keep normal EF construction, mappings, migrations, saves
and commits visible. Do not add an ambient unit of work or cross-module transaction.

Template output: ordinary DbContext/model/migration registration. Add the PostgreSQL CI lane
for active code in this increment; archived lane remains distinct.

### E3 Trusted ingress and state-stored sample path

Build the smallest real Access-to-business-module journey using the new context seam and
persistence utilities. Keep BFF/OIDC, memberships, permissions, Minimal API endpoints and
CQRS code in the sample. Freshly record module ownership and domain language. Exercise two
users/tenants, current membership and antiforgery behavior through real ingress.

Review membership and tenant admission as a separate Access increment: an application user
can belong to multiple Organizations, and admission concerns the Organization selected for
this operation. Prove accepted and denied membership, independent operation scopes for the
same actor in different Organizations, and the consumer's revocation policy. Membership
management, invitations and role/permission behavior receive further increments as needed;
none becomes part of the actor-identity or tenancy core.

This may be several reviewable sample-only increments. A ticket-store or policy utility
becomes a separate library only if its comparison removes meaningful repeated complexity.
The review proposes independently adoptable ASP.NET Core actor and tenancy adapters: explicit
consumer identity/tenant resolution, one initialization per segment, and configurable tenancy defaults with endpoint/group
exceptions. The template defaults to native authentication requirements plus a required
tenant. Native authorization decides caller access, with `AllowAnonymous` working without
an extra actor opt-out. Do not introduce a separate HTTP actor policy or actor-specific
endpoint extension. Propose separate tenantless metadata as the tenant exception. Tenancy
defaults must survive named authorization policies; native named policies own their intended
authentication requirements. Resolve authenticated principals to application actors without
silently downgrading mapping failures to anonymous execution.
Review the resolver and error/selection interfaces with real HTTP proofs, including metadata
precedence, anonymous tenant access, identified tenantless access, authentication schemes,
native challenges, denied admission, pipeline ordering, login/callback/health paths and
independent adoption. Include unmapped authenticated principals, authenticated callers on
anonymous endpoints, and native policies deliberately permitting anonymous access. Resolve
tenant identity/admission before initializing the tenancy context;
do not mutate it later. Provider-specific authentication, membership/admission and HTTP failure
policy stay consumer-owned; generic context establishment can be reusable without embedding
that policy. Introduce an editable template-local native cookie/OIDC registration helper here;
focused authentication library utilities require subsequent reuse evidence. The details and
native-policy references are in [the HTTP integration review](../design.md#scope-and-http-integration-review).
Native host ServiceDefaults/AppHost/Migrator are sample/template composition. Add Topology
coverage with actual identity/session wiring, not a production fake actor. This establishes
that state-stored modules work without event sourcing or messaging.

### E4 Durable event identity and payload codec

Compare Inventory and Purchasing stable identities, required/optional payload behavior,
error classification and serializer configuration. Replace their repeated technical logic
through a small library exercised by both new sample event families. Explicit registration
is preferred over discovery that quietly registers additional types.

Use retained literal JSON plus independently expected state/results. Prove unknown and
conflicting identities, unsupported schemas, missing required fields, deliberately optional
fields and CLR rename independence. Do not invent a v2 event or generic upcaster to justify
an interface. The codec consumer builds without EF or messaging. This slice need not add a
new template beyond explicit event registration.

### E5 Event history and native append

Deliver ordered-history/hydration and stream/envelope staging as independently reviewable
increments if necessary. Integrate both aggregate families using the owning module's native
EF transaction. Domain deciders/reducers and state shapes stay consumer-owned.

Prove captured-head contiguous history, version/time selectors and regression/corruption
classification, competing append, stream/event-write faults, replay with no external effect,
and caller-owned save/commit. Neither API promises projection-independent business-key
uniqueness. An event-sourced consumer runs without messaging; state-stored composition is
unchanged. Add no generic aggregate repository or compulsory DDD base type.

### E6 Required inline views and bounded repair

Integrate an aggregate-shaped write view and an independent summary view with one atomic
append. Prove failure of each participant rolls back all required views/events/audit, with
independently expected quantities and totals. Missing/lagging required views fail according
to explicit consumer policy; ordinary append does not silently repair them.

Review Inventory-style repair separately. Keep privileged admission, identity discovery,
lock lifetime and stream-creation coordination visible. Full reconstruction remains bounded
maintenance until measured evidence justifies another mechanism. Purchasing repair requires
its own implementation and proof before it can be claimed. Async projections, checkpoints,
shadow reconstruction and leader election are outside this extraction increment.

### E7 Reliable messaging storage and callable dispatch

Compare Sales, Inventory and Purchasing inbox/outbox shapes. Stage receipts/outgoing work in
the caller's module transaction. Preserve the distinction between delivery identity and the
consumer's semantic operation identity. Review lease claims, token checks, clock source,
retry/backoff policy, retention and identity-preserving redrive.

Separate one callable dispatch operation from the optional BackgroundService loop. The
consumer supplies publication/routing; the worker registers only when selected. PostgreSQL
proofs cover competing claims, expired lease takeover and stale claimant completion. A
consumer runs this segment without event sourcing; event sourcing runs without it.

### E8 Transport integration and durable sample round trip

Add explicitly wired Rebus/RabbitMQ handlers and publishing adapters; the consumer owns
endpoint creation, queue/topic names, subscriptions, routes and error/retry policy. Rebuild
reservation/shortage/release through sample Contracts, preserving stable operation identities.
Sales owns durable process transitions and compensation. Reconstruct before/after-commit,
pre-ACK, duplicate, reordered, poison and competing-replica scenarios from archived evidence.

Review delivery atomicity and ambiguous external outcomes with real PostgreSQL/RabbitMQ.
Keep process-local retry limits explicit. Publication cancellation/stalled-confirmation and
shutdown under unavailable telemetry must be proven before making bounded-shutdown claims.
Do not hide unsupported transport cancellation behind a token parameter. A generic saga
library remains a later candidate, not the outcome of a working Sales process manager.

### E9 Audit and remaining utility decisions

Compare audit envelopes across human and workflow consumers. Extract technical storage or
staging only if it removes meaningful duplication without automatic auditing. Accepted-change
audit participates in the native module transaction; denial audit has explicit semantics.
Keep payload classification, action/reason identifiers, retention and query policy local.

Reassess DDD utilities, email, authentication, configurable architecture-test helpers and
workflow mechanics individually. Omit candidates that merely rename native APIs or require
large consumer setup to hide little complexity. This increment is a decision gate, not one
large library PR; each selected mechanism receives its own consumer/proof slice.

### E10 Template rehearsal and delivery basis

Consolidate exercised sample setup into the template and create a second consumer with
selected segments. Verify build, migrations, local startup, independent adoption and CI.
Naming/configuration stays bounded by actual exercised choices. Scan durable aliases/schema/
route names deliberately.

Plan a bootstrap CLI that applies selected template code and configuration to a repository.
Future selections may include RabbitMQ or Service Bus, optional event sourcing, selected
Aspire resources and OIDC/Keycloak setup with directory-gated or open registration. These
are intended configuration axes; implementing every alternative is not required by the
initial extraction. Add supported options as the corresponding implementations and real
consumer compositions are proven. Materialized files remain consumer-owned and reviewable.

Before implementing the CLI, review its supported compositions, input/configuration format,
existing-file conflict behavior and repeat-invocation semantics. Verify each supported
composition and relevant interactions; reject unsupported combinations explicitly. Prove
that omitted capabilities do not leave required packages, workers, resources or registration
behind. Template-time selection preserves explicit runtime wiring and provider-specific
features; it does not require a universal provider interface or runtime code generation.

Add the selected dependency-update policy and, when frontend work starts, a pinned pnpm
workspace with actual shared subpackage usage. OCI packaging, real Service Bus/production
OIDC compatibility, deployment and restore/rollback require separately planned evidence.
Extraction does not inherit the old Azure pilot as a library requirement or certify it.

## Proof transfer and unresolved limits

| Archived finding | Requirement for the new implementation |
| --- | --- |
| Business-key lookup depends on rebuildable views | Preserve the limitation explicitly, or prove a consumer-owned durable identity constraint before claiming uniqueness through projection loss. Never interpret missing view as universally safe creation. |
| Inventory repair uses an Organization-wide cooperative gate | Retain the tested coordination scope, or prove a replacement covering existing writers, new streams and lock ordering. Native advisory locks do not constrain arbitrary bypass SQL. |
| Purchasing has no projection repair | Live reconstruction is a read, not repair. Add and prove a real owning-module repair before supporting deleted-view recovery. |
| Retained reservation children increase replay/append cost | Keep state shape outside the library. Measure real payload/state mixes; event count alone is not a capacity guarantee. |
| Stream, views, audit and workflow receipt/outbox share a native transaction | Repeat failure of each required participant through the new consumer wiring. No staging method independently commits or publishes. |
| Delivery can repeat and external outcomes can be ambiguous | Preserve semantic identities and idempotent effects; timeout or absent response does not establish business failure. |
| Technical retries are process-local | Do not advertise a global attempt cap. A different policy requires competing-replica and restart evidence. |
| Stalled publish and exporter shutdown remain unproven | Resolve with actual fault tests before promising a latency/shutdown bound or deployment readiness. |
| Shared cross-module transactions were deferred | Add a named workflow and real shared-connection/enlistment/failure proof as a separate increment if required. |
| Snapshot-plus-tail has one concrete late consumer | Keep bootstrap ordering/checkpoint policy in the sample until another real consumer earns a library. |

Archive test counts and measurements remain historical. New test runs must identify the
new interface and consumer configuration they actually exercise. Keep retained fixtures and
independent semantic expectations; avoid implementation-equivalence-only assertions.

## Review and completion

For each increment, review public types and consumer obligations first, then implementation,
consumer wiring, tests, and documented limits. Report files worth line-by-line review and the
complexity removed. All changes remain unstaged; every commit needs exact-change-set approval.

Completion of extraction requires a working sample using the libraries through project
references, passing capability-specific proofs, tested independent adoption, and exercised
template creation. Candidate inventory and matching archived results alone do not complete
that goal. Only guarantees exercised by the active implementation and documented in its
report count as new reusable proofs; later increments remain proposals.
