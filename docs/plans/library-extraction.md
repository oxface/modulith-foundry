# Library and sample extraction plan

## Current delivery status and next slice — 2026-10-07

The three outputs remain reusable libraries, configurable repository population, and samples.
[The strategy review](../reports/strategy-review.md) brought a bounded template rehearsal
forward from E10; that rehearsal is now complete and owner-approved.

| Capability | Current disposition |
| --- | --- |
| E1–E3 foundations and ingress | Retained checkpointed libraries and consumer proofs. |
| E4–E5 event utilities and native consumers | Retained checkpointed mechanisms and experiments; no replacement dependency selected. |
| E6.1 inline decision state | Checkpointed separately as `1ae13d4`; an experiment, not justification to proceed automatically to repair. |
| T1 state-stored template rehearsal | Owner-approved checkpoint `8ccf4c8`; two generated consumer proofs, event/messaging omission and bounded initial creation. |
| ES1 bounded event append | Owner-reviewed aggregate/store replacement implemented. [Concrete interface/scope](es1-bounded-event-append.md#concrete-aggregate-based-replacement-for-interface-review) and [new proofs](../reports/es1-bounded-event-append.md) accompany line-by-line implementation review. |

T1 implements one fixed event-free Catalog/console composition with configurable application
name/root namespace, local library source snapshots and TypeScript/npm creation tooling around
native `dotnet new`. See [the creator](../../tools/template/README.md) and
[checkpoint findings](../reports/t1-template-rehearsal.md) for supported platforms and limits.
It does not implement optional event presets, production ingress or repository updates.

ES1 concentrates accepted-batch technical append using the existing state-loading paths.
Inventory issues depend on loaded availability; the independent counter uses captured history
and direct JSON without tenancy or required views. Domain policy and native save/commit remain
consumer-owned. [Its report](../reports/es1-bounded-event-append.md) distinguishes new proofs
and source-concentration leverage from historical evidence. Existing demos, fixtures,
migrations, the frozen archive and T1 output are preserved; no cleanup is included.
Marten remains a behavioral reference for a smaller optional capability, not a selected
runtime dependency. The approved native EF direction remains in force.

The owner requested and approved replacement of per-call delegates/timestamp assembly with
an aggregate-oriented journey. The package-free aggregate core and configured EF appender
are now implemented; [ADR 0005](../adr/0005-aggregate-write-contract-and-native-append.md)
records the required write contract and optional inheritance. Pure reducers remain in modules
and are shared with existing historical reconstruction. The revised report records actual
verification separately from the initial surface. No generic reader/projector engine, repair
or subsequent event slice is authorized.

The [owner follow-up and deferred capability catalog](event-sourcing-capabilities.md) records
transactional main-state consistency, native query/filter naming, event registry/JSONB findings
and future proof obligations. Mandatory main state applies to registered aggregate writes;
raw streams remain permitted. The owner subsequently endorsed IEventStore<TAggregate> and
authorized concrete changes. [The provided write-store surface](es1-library-write-store.md)
now concentrates native lookup, version observation/comparison, context/transaction association,
main-state loading and configured required-state staging. Explicit native save validation
checks tracked required participants; no generated persistence or hidden save/commit.
[New executions](../reports/es1-library-write-store.md) accompany implementation review.

The owner-approved default envelope and projection/store terminology refinements are now
implemented. [Library-local capability records](../../src/ModulithFoundry.EventSourcing/ModulithFoundry.EventSourcing.EntityFrameworkCore/docs/capabilities.md)
retain the supported contract, missing rebuilding/async/multi-stream context and provider
adapter investigation. Other packages retain their own local setup/limits/deferred directions.
The default-envelope refinement did not move projects; see [its proof and scope report](../reports/es1-envelope-and-library-docs.md).
The subsequent owner-authorized [family relocation](library-family-layout.md) groups all five
library families with local documentation and tests. No Postgres locking package is implemented.

Stop after this reviewable ES1 capability. The E0–E10 sections below preserve
the original sequence and evidence; they are not an instruction to begin E6.2 or E7.

## Checkpoint history

Status: review proposal, 2026-10-03. The owner approved the archive-and-plan direction.
This document proposes implementation increments; it does not freeze public interfaces,
package boundaries, or authorize a commit. E1 was reviewed and checkpointed as `a8e45c9`;
[the checkpoint report](../reports/e1-tenant-actor.md) records that original combined design.
The independent split was reviewed and checkpointed as `c8cbf64`;
[its current report](../reports/e1-identity-split.md) records fresh proofs.
E2.1 was [reviewed and checkpointed as `50e7933`](../reports/e2-1-tenant-ownership.md).
[E2.2 module-owned migrations](e2-2-module-migrations.md) were checkpointed as `f2dcf2b`.
[E2.3 tenant relationships](e2-3-tenant-relationships.md) were checkpointed as `d67c7fc`.
[E2.4 versioned profile changes](e2-4-versioned-profile-changes.md) were checkpointed as
`6069c05`, completing the initial supported E2 scope. Shared cross-module transactions and
[the remaining E2 limits](e2-persistence.md) require separate evidence.
[E3.1 actor HTTP integration](e3-1-http-actor-identity.md) was owner-reviewed and checkpointed
as `faefc0b`; [its report](../reports/e3-1-http-actor-identity.md) records cookie/policy/
claim-action proofs and the registration refinement.
[E3.2 tenancy HTTP integration](e3-2-http-tenancy.md) was owner-reviewed and checkpointed as
`cfbac9a`; [its report](../reports/e3-2-http-tenancy.md) records fresh adapter,
Organization/Inventory consumer and independence proofs.
[E3.3 persisted Access lookup/admission](e3-3-persisted-access.md) was owner-reviewed and
checkpointed as `20a02be`; [its report](../reports/e3-3-persisted-access.md)
records actual PostgreSQL/HTTP consumer evidence.
[E3.4 persisted business ingress and module projects](e3-4-persisted-business-ingress.md)
was owner-reviewed and checkpointed with E3.5 as `31c7a8b`;
[its report](../reports/e3-4-persisted-business-ingress.md) records actual integration proofs. Read
[the approved design posture](../design.md) alongside this plan.
[E3.5 Sales profile mutation](e3-5-profile-mutation.md) was owner-reviewed and checkpointed as `31c7a8b`;
[its report](../reports/e3-5-profile-mutation.md) records native antiforgery, concurrency and rollback proofs.
[E3.6 runtime composition](e3-6-runtime-composition.md) was owner-reviewed and checkpointed
as `28ee797`; [its report](../reports/e3-6-runtime-composition.md) distinguishes
runtime and native exporter proofs from manual dashboard observations.
[E3.7 real OIDC/browser journey](e3-7-oidc-browser-journey.md) was owner-reviewed and
checkpointed as `dc3ac3b`; [its report](../reports/e3-7-oidc-browser-journey.md) records optional
local Keycloak, explicit account mappings and actual Chromium journeys.
[E4 event serialization](e4-event-serialization.md) was owner-reviewed and checkpointed as
`2a49ef3b`; [its report](../reports/e4-event-serialization.md) records fresh two-family codec,
compatibility and independent-adoption proofs. [E5.1 history/hydration](e5-1-event-history.md)
was owner-reviewed and checkpointed as `4cc12a1`; [its report](../reports/e5-1-event-history.md)
records selected-range integrity and two-family reconstruction proofs. Its package division
remains for review alongside the now-implemented [E5.2.1 native EF consumers](e5-2-1-native-event-history.md).
[Their report](../reports/e5-2-1-native-event-history.md) records new PostgreSQL evidence. E5.2.1
was owner-reviewed and checkpointed as `4f4d5b2`. [E5.2.2](e5-2-2-native-event-append.md) is
owner-reviewed and checkpointed with E5.3 as `abcd370`; [its report](../reports/e5-2-2-native-event-append.md)
records new native append evidence and no new library mechanism.
[E5.3 explicit storage registration](e5-3-event-storage-registration.md) is implemented before
E6 and checkpointed as `abcd370`. [Its report](../reports/e5-3-event-storage-registration.md) records
the extracted stream/envelope model utility independently of append orchestration.
[E6.1](e6-1-inline-decision-state.md) was checkpointed as `1ae13d4`;
[its report](../reports/e6-1-inline-decision-state.md) records inline decision state, atomic required
views and no new reusable mechanism.
[T1](t1-template-rehearsal.md) was owner-approved and checkpointed as `8ccf4c8` after review
corrections; [its report](../reports/t1-template-rehearsal.md) records creation and adoption
proofs, with no new reusable runtime mechanism. The ES1 interface/scope was then owner-approved
on 2026-10-06; its implementation remains unstaged for review, with no commit authorized.

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

### Current layout and extension direction

- `src/ModulithFoundry.{Family}/`: family README/docs, independently selectable package
  directories and library tests. ActorIdentity, Tenancy, Persistence, Events and EventSourcing
  are grouped without adding runtime dependencies.
- `samples/Wholesale/`: API composition root, modules/Contracts, finite Migrator, AppHost,
  host ServiceDefaults, and sample-specific tests, introduced as needed.
- `tests/`: repository-wide architecture checks and shared test support; library-interface
  and independence tests live in the owning family's `tests/` directory.
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
| Event-sourcing model registration | Explicit EF stream/envelope mapping, version token, selected keys/relationship and position uniqueness; E5.3 owner-reviewed implementation | Consumer row types, ownership/filter choice, DbContext/provider, migrations, stream families and saves. No required tenancy or codec dependency. |
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

Start with [E3.1's approved actor-only HTTP adapter scope](e3-1-http-actor-identity.md): effective
native principal mapping, explicit evaluator/middleware composition and focused request
proofs. It was owner-reviewed and checkpointed as `faefc0b`, including explicit registration
helpers and a native cookie/OIDC sample recipe. Remote provider topology is unproven.

[E3.2's tenancy HTTP interface and scope](e3-2-http-tenancy.md) were reviewed and checkpointed
as `cfbac9a`. The owner confirmed
middleware after native authorization and actor establishment, before endpoint work. This
first adapter enforces tenant defaults/metadata and consumer resolution/admission without a
second policy evaluator; tenant-aware native authorization handlers require a later slice.
Optional route/subdomain candidate helpers and independently adoptable tenancy-only proofs
now accompany the extended actor/tenant HTTP sample.
[The report](../reports/e3-2-http-tenancy.md) records actual results. Durable Access is not part of E3.2.

[E3.3 persisted Access lookup/admission](e3-3-persisted-access.md) was checkpointed as
`20a02be`: global user and
external-identity lookup, canonical Organization lookup, current membership admission,
explicit public catalog policy and admission-time revocation semantics on real PostgreSQL.
The owner approved these policies; [the report](../reports/e3-3-persisted-access.md) records
the implementation proofs. Technical libraries are unchanged and no new reusable mechanism
was extracted. Editable Access remains sample/template code.
Inventory remained a fixture in E3.3. The checkpointed
[E3.4 persisted business ingress and module projects](e3-4-persisted-business-ingress.md):
read-only tenant-owned Inventory data using E2, populated Access/Inventory implementation
and Contracts projects, host-owned HTTP bridges, separate histories and explicit setup.
The owner approved this scope; [the report](../reports/e3-4-persisted-business-ingress.md)
records actual reads, migration compatibility and independent-consumer results.
[E3.5 Sales profile mutation](e3-5-profile-mutation.md) is checkpointed: native antiforgery for cookie
JSON mutations, application-actor token binding, explicit native Sales saves/transaction,
expected-version conflicts and rollback after a partial write. The owner selected this
business capability and approved its interface and token/authority policies.
[Its report](../reports/e3-5-profile-mutation.md) records the new HTTP/PostgreSQL path, including controlled precommit cancellation.
These are consumer increments; no new library mechanism is assumed.

Reassess focused native authentication/antiforgery utilities when real ingress exposes a
repeated mechanism. Actor/principal mapping and tenant admission remain editable consumer
adapters. The owner narrowed the antiforgery candidate to an optional human-actor requirement;
the current native token binding/filter/HTTP setup stays editable template code. No utility
was extracted and neither core gains a dependency.
Membership administration, invitation acceptance, explicit account linking and permissions
may also earn an optional Access feature module. Revisit that extraction gate when their
actual workflows and alternative policies exist; it need not wait until E9.

E3.6 runtime composition/telemetry is checkpointed. [E3.7](e3-7-oidc-browser-journey.md)
implements disposable OIDC hosting and real browser login/callback, mapped actor, admission
and protected profile mutation, checkpointed as `dc3ac3b`. This closes the bounded E3 ingress
scope; [E4 durable event identity and payload codec extraction](e4-event-serialization.md)
is checkpointed as `2a49ef3b`, before E5 history/append.
E3.7 adds no technical
library or provider-neutral authentication abstraction. Its local HTTPS topology does not
prove external cross-site providers, proxy/subdomain sessions or account administration.
E3.7 supplied exercised sample recipes. T1 now materializes a separate bounded state-stored
composition; generating this richer HTTP/OIDC composition remains later work.
Native ServiceDefaults remains template source; the owner confirmed it warrants no Foundry
library wrapper.

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

[The approved E4 scope](e4-event-serialization.md) compares the two archived serializers
and defines the native JSON codec, standalone two-family consumer, dependency promise and
relevant proofs. `ModulithFoundry.Events.Serialization` was owner-reviewed and checkpointed
as `2a49ef3b`; [the report](../reports/e4-event-serialization.md) records actual
bounded guarantees without claiming event-store behavior.

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

[E5.1](e5-1-event-history.md) implements metadata-only ordered-range validation and explicit
two-family hydration, owner-reviewed and checkpointed as `4cc12a1`. Consumer queries own
version/time selection;
the initial complete-history snapshot/selection object was rejected during review.
[Its report](../reports/e5-1-event-history.md) separates the new mechanism from consumer policy.
E5.2 is split into two reviewable capabilities:

- [E5.2.1 native EF history reads](e5-2-1-native-event-history.md): integrate both families
  into owning modules, map streams/envelopes with native migrations, exercise tenant-scoped
  version/time queries and bounded captured-head reads on PostgreSQL. Use explicit finite
  setup writes; this is not an append protocol. Owner-reviewed and checkpointed as `4f4d5b2`;
  [the report](../reports/e5-2-1-native-event-history.md) records new proofs.
- [E5.2.2 native append](e5-2-2-native-event-append.md): expected-version staging,
  caller-owned save/commit, competing
  writers, stream/event-write faults, rollback and fresh-context recovery. Owner-reviewed and
  checkpointed with E5.3 as `abcd370`. [The report](../reports/e5-2-2-native-event-append.md) compares the
  consumer-owned writers: native EF supplies the concurrency/transaction mechanism. Reassess
  the repeated staging shape with E6's required participants before proposing another interface.

E5.1 does not establish database capture or transaction guarantees. E5.2.1 proves native
read composition under the documented append-only assumption, adds no new reusable mechanism
and recommends retaining the small independent History library for owner review. E5.2.2 now
proves append atomicity, conflicts and rollback for the explicit module protocol on PostgreSQL.
Its initial E5 read/append scope is implemented; required views, audit and messaging have not
yet participated in these transactions.

Prove captured-head contiguous history, version/time selectors and regression/corruption
classification, competing append, stream/event-write faults, replay with no external effect,
and caller-owned save/commit. Neither API promises projection-independent business-key
uniqueness. An event-sourced consumer runs without messaging; state-stored composition is
unchanged. Add no generic aggregate repository or compulsory DDD base type.

### E5.3 Explicit event-sourcing storage registration

[The E5.3 scope](e5-3-event-storage-registration.md) extracts a narrower capability
before E6: duplicated technical stream/envelope mappings in the two active modules. Implemented
one EventSourcing.EntityFrameworkCore library with native EF Relational, consumer-owned row
interfaces and explicit model registration. A tenant-free overload uses ordinary identities;
native key expressions support the current owned keys. Ownership filters and jsonb mapping
remain explicit consumer configuration; no actor, tenant, codec or messaging dependency.

The owner authorized the interface before implementation. [The report](../reports/e5-3-event-storage-registration.md)
records new adoption proofs: preserved module schemas/migrations and append behavior, multiple
StreamType values in one table pair and customized tenant-free independent usage. The new
mechanism is model registration; it does not enforce append-only behavior or commit transactions.
Implementation was owner-reviewed and checkpointed as `abcd370`. Append coordination remains
a separate candidate evaluated with E6 evidence.

### E6 Required inline views and bounded repair

[E6.1 inline decision state and required views](e6-1-inline-decision-state.md) was checkpointed
as `1ae13d4`. [Its report](../reports/e6-1-inline-decision-state.md) records earlier proofs of ordinary
edits without history reads, complete-batch validation, independent summary evolution and
atomic event/header/view updates on PostgreSQL. No new reusable mechanism was proven: local
immutable candidates suffice, and concrete view checks/staging do not yet justify a generic
projector interface. Explicit Contracts, mappings and orchestration are editable template
recipes; T1 materializes an event-free composition, not these event recipes. Bounded repair
requires a separate E6.2 scope and is not the next slice.

The broader E6 objectives below remain the gate for subsequent increments.

Before proposing the E6 interface, compare the archived aggregate wrappers, deciders,
candidate-state policies, live readers and inline projectors with the Marten reference.
Record that comparison in [the event-sourcing reference review](../reports/marten-event-sourcing-reference.md).
The E5.3 mapping proof does not settle aggregate loading, projection execution or repair.
Do not infer that these capabilities have no reusable mechanics from native EF providing
the transaction and concurrency primitives.

First establish one reviewable decision-and-inline-view capability in both aggregate families:

- Keep command eligibility and complete-candidate invariant validation separate from historical
  evolution. Accept a batch only after its final candidate is valid; a rejected batch must
  not change accepted state or pending events. Aggregate wrappers remain optional consumer code
  unless their bookkeeping earns a separately reviewed utility.
- Load current decision state from an aggregate-shaped inline write view and compare its
  version with the stream registry. Keep live reconstruction available as an explicit read;
  demonstrate that ordinary editing can succeed without replaying the whole history.
- Apply accepted events to explicitly selected inline views. An independent summary owns its
  reducer and advances from its own committed state. Calculating an in-memory preview, staging
  inline rows, and repairing persisted rows are separate operations.
- Compare repeated expected-version checks, accepted-batch handling and required-projector
  coordination before deciding whether to extract a callable utility. Consumers retain projector
  definitions, persistence mappings, required-view policy and the final save/commit. No assembly
  discovery, generated projection code or SaveChanges interception is proposed.

Integrate an aggregate-shaped write view and an independent summary view with one atomic
append. E6.1 proves rollback of required views/events/header. Audit needs its own participant
and failure proof before event/view/audit atomicity can be claimed. Verify results with
independently expected quantities and totals. Missing/lagging required views fail according
to explicit consumer policy; ordinary append does not silently repair them.

Review Inventory-style repair separately. Keep privileged admission, identity discovery,
lock lifetime and stream-creation coordination visible. Full reconstruction remains bounded
maintenance until measured evidence justifies another mechanism. Purchasing repair requires
its own implementation and proof before it can be claimed. Async projections, checkpoints,
shadow reconstruction and leader election are outside this extraction increment.

Keep async projections as a later capability candidate requiring an actual eventual-consistency
consumer and its own scope. A global event position alone does not establish committed ordering
or safe progress. Explicit callable processing, progress/effect atomicity, gaps, competing workers
and replay without external effects need new proofs before supporting that execution model.
Metadata, checkpoint snapshots, projection revisions and pending-event preview likewise remain
separate candidates, rather than compulsory fields or services in E5.3 storage registration.

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

The first bounded creation/adoption rehearsal was delivered early as T1 (`8ccf4c8`). Its
initial-creation behavior and fixed state-stored composition are supported now. The objectives
below concern further supported compositions and delivery, not a requirement to repeat T1.

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
