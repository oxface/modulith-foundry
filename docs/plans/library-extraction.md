# Library and sample extraction plan

Status: review proposal, 2026-10-03. The owner approved the archive-and-plan direction.
This document proposes implementation increments; it does not freeze public interfaces,
package boundaries, or authorize a commit. No new runtime library is implemented in this
checkpoint. Read [the approved design posture](../design.md) alongside this plan.

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
| Tenant and actor context | Independent identity/context seam and explicit scope validation | Membership, roles, invitation lifecycle, tenant admission, actor trust and authorization remain consumer policy. This is an agreed early library direction. |
| Module persistence | Explicit EF ownership-filter/model utilities and justified write validation | Module DbContext, schema, mappings, migrations and transaction ownership. The archived `shared/Persistence` utility is a starting comparison, not a required base context. |
| Event identity and codec | Explicit alias/version registry, payload encoding/decoding and compatibility errors | Event definitions, required/optional fields, allowed schemas and evolution. Compare both serializers before designing a common interface. |
| Event history | Contiguous ordered-range checks, captured-head reads and deterministic hydration mechanics | Domain reducer, state shape and temporal meaning. Preserve application append time versus commit time. |
| Event append | Stream expected-version checks and event/envelope staging in native EF transactions | Business-key identity, decision state, view definitions, audit and transaction owner. PostgreSQL guarantees stay explicit. |
| Inline projections and repair | Explicit batch coordination and bounded reconstruction helpers where genuinely shared | View identities/reducers, required-view policy, write admission, lock granularity and privileged recovery. Purchasing repair is not yet proven. |
| Reliable messaging | Inbox/outbox storage, lease claims, token-guarded completion/backoff and callable dispatch | Semantic operation identities, fingerprint meaning, producer trust, retention, routes and payload mapping. Delivery deduplication is not business idempotency. |
| Rebus/RabbitMQ integration | Optional adapters around demonstrated delivery/publishing behavior | Consumer creates endpoints and wires queues, topics, subscriptions, handlers, routing and retry/error policy. No generic bus registration facade. |
| Audit | Common technical envelope and explicit staging, if comparison earns a library | Action/reason vocabulary, denial policy, sensitive details, retention and query visibility. Event streams are not security audits. |
| Authentication | Evaluate focused technical utilities such as ticket storage | BFF/OIDC settings, principal completion and Access behavior begin as sample/template code. No general identity framework is assumed. |
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

A tenant/actor library has no EF, web, transport, Aspire, or sample Access dependency.
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

This is the completed archive-and-plan checkpoint; its changes await owner review.

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

### E1 Tenant and actor seam with a minimal sample

Review immutable tenant/actor context, identity representation and explicit scope lifetime.
Compare passing context explicitly with a fresh DI scope; choose the smaller interface that
handles both human and workflow consumers. Tenant selection never asserts membership or
permission. System actor identities come from trusted consumer wiring, not caller input.

Provide a runnable sample host and two real capability calls exercising isolated contexts,
with concurrent operations and missing/mismatched context cases. Test setup actors are not a
production authentication implementation; expose tenant business HTTP operations only after
trusted ingress is in place. Prove no cross-scope leakage, allowed/forbidden scope reuse and
cleanup on exceptions/cancellation. Build a minimal consumer without EF, ASP.NET Core, Rebus,
Aspire or sample Access.Contracts. Add active solution, Fast lane and hook coverage.

Template output: explicit context establishment and lifetime recipe if this is a new setup
pattern. Domain policy stays in the sample.

### E2 Explicit EF tenant and module persistence utilities

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

This may be several reviewable sample-only increments. A ticket-store or policy utility
becomes a separate library only if its comparison removes meaningful repeated complexity.
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
Naming/configuration stays bounded by actual exercised choices; no runtime code generation
or provider matrix is introduced. Scan durable aliases/schema/route names deliberately.

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
that goal. No new reusable mechanism is proven by this planning checkpoint.
