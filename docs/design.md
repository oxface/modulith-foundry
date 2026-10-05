# Library and template design decisions

Status: direction approved by the owner on 2026-10-03. Public interfaces, package divisions,
and implementation increments in [the extraction plan](plans/library-extraction.md) remain
review proposals.

## Purpose and ownership

Build useful .NET libraries, a consumer-owned repository template, and a working sample.
Use the archived wholesale sample to identify guarantees and failure cases; reimplement
mechanisms when that gives a clearer design. Domain completeness is not the goal.

The template demonstrates one application composing business modules with explicit
Contracts, internal implementations, and separately owned persistence. Architecture checks
and policy middleware are reusable, opt-in tools with consumer-selected policies; the
library does not prescribe every consumer's project graph or module structure.

Authentication establishes identity. Independent actor-identity and tenancy contexts have
no dependency on each other or the sample Access model and do not grant authorization.
Membership, invitation lifecycle, roles, permissions, and business authorization belong
to the consumer.

## Current adoption strategy

For now, a library segment must also be usable in an ordinary .NET API or worker without
adopting the template's module layout or Access model. The template supplies a coherent
composition; standalone consumers receive the segment's documented guarantees through their
own explicit wiring. This is a current delivery choice, open to revision when implementation
evidence demonstrates value in more involved libraries.

Access starts as consumer-owned sample/template code, including the product's user,
Organization, membership, invitation and role behavior. Consumers can copy and customize it.
Shipping it in the template preserves a practical path to later extraction; it does not make
the Access model a dependency of the tenant/actor seam or other technical libraries.

Revisit either choice through [the plan's extraction and strategy gate](plans/library-extraction.md#extraction-and-strategy-gate).
The trigger is demonstrated reuse or complexity removed, followed by review of added policy,
coupling, customization and proof obligations. More involved integration is a possible outcome,
not a requirement to add speculative abstractions now.

## Tenant and actor meaning

Tenantless execution is an explicit choice, distinct from failing to establish a required
tenant. Each capability declares whether tenantless execution is permitted; operations that
require a tenant reject missing context. Login or tenant creation can run outside a tenant
boundary. Tenantless execution does not grant access to every tenant's data.

Keep the executing actor separate from the initiator. For deferred work performed by a
named workflow/system identity, that identity is the actor; the human who triggered it can
remain its initiator. Attribution grants no authority. Whether work continues after the
initiator loses membership or permission is an explicit consumer policy, not a decision made
by the context library. Consumers establish trusted actor identities and authorize operations.

Anonymous execution is explicitly represented, distinct from missing actor context. Consumer
policy decides which capabilities allow it; capabilities requiring a trusted human or
system actor reject anonymous or missing actor context. Anonymous and tenantless execution
are independent choices: a public operation may have a selected tenant, and an identified
actor may perform tenantless work. Context establishment itself grants no authority.

Tenant, actor and initiator stay fixed throughout one operation. Internal module calls may
share that immutable context. A worker processing different tenants or acting identities
establishes a separate operation context for each; it does not rebind its current context.

Use opaque string keys carried by distinct tenant/actor value types. Consumers explicitly
map their domain identities into these keys; the library does not prescribe GUIDs or depend
on authentication-provider identity types. Compare keys exactly using ordinal comparison.
Reject empty and whitespace-only keys; preserve accepted values without trimming, case
conversion or parsing. Consumers own canonicalization at the mapping point.
The representation trade-off is recorded in [ADR 0001](adr/0001-opaque-operation-identities.md).

Start the sample/template with a stable, globally unique application UserId and external
identities resolved by consumer-owned Access code. For OIDC, resolve validated issuer and
subject to that UserId; human actor keys carry the resulting application identity. Provider
resolution, account linking and email-based admission remain Access policy. The technical
library does not require provider/issuer fields or a User entity. Human and system actor
identities remain distinguishable even when their key text matches.

Operations obtain context through an injected, read-only accessor. The immutable context
value remains independent of DI, EF, web and transport types. Consumers wire context
establishment and accessor lifetime explicitly. The host initializes each required segment
exactly once per operation scope through its separate initialization interface. Reading before
initialization or initializing again is an error. Reset, replacement and automatic fallback
to anonymous context are excluded. Business code depends on the read interface; this
separation expresses consumer roles and does not replace application authorization.

Initiator attribution is optional. Consumers supply it when known; the library neither
infers it from the current actor nor automatically propagates it to another operation.
Scheduled maintenance and work with unavailable original attribution can omit it.

Once initialized, each accessor supports concurrent reads of its immutable context within
one operation. Required establishment finishes before parallel business work begins; separate
operations have separate scopes. This guarantee covers context reads and does not extend
the concurrency guarantees of other injected dependencies.

Provide explicitly called checks for context requirements, such as a selected tenant or
an identified actor. Consumers choose where to apply those checks and how to present
failures. The checks do not resolve membership, authenticate an identity, decide permission
or select HTTP responses.

The revised E1 interfaces and lifecycle are in
[the actor/tenancy slice plan](plans/e1-tenant-actor.md). The original combined interface was
reviewed and checkpointed as `a8e45c9`; the independent split was owner-reviewed and
checkpointed as `c8cbf64`. See [the split report](reports/e1-identity-split.md) for fresh proofs.

## Scope and HTTP integration review

`ModulithFoundry.ActorIdentity` covers executing actor kind/key, optional initiator, its
accessor lifecycle and explicitly invoked identity-presence checks. Actor denotes attribution,
not the actor concurrency model. `ModulithFoundry.Tenancy` covers selected tenant or deliberate
tenantless execution, its accessor lifecycle and selected-tenant checks. Both are package-free
and independent. Transaction, trace, HTTP and module-persistence contexts have their own seams.
Authentication, membership and authorization remain consumer behavior.

Capabilities needing both explicitly check both accessors. Each segment is established once;
there is no atomic publication across them. Hosts finish establishment of all segments needed
by a capability before invocation; an establishment failure aborts the operation and the host
disposes the scope. Consumers may establish actor identity first for tenant admission lookups.
Actor identity is kind plus canonical key; Access resolves profiles, external identities,
memberships and permissions without adding them to the contexts.

The HTTP direction below must be implemented with independently adoptable actor and tenancy
adapters; tenancy selection does not require the actor library. Consumer resolvers can compose
both when membership needs an application actor. Neither adapter is implemented in E1.

For HTTP usage, propose explicitly registered ASP.NET Core adapters with context
establishment middleware and consumer-selected tenancy defaults. The template should
require authentication through native authorization configuration and a selected tenant
through the tenancy adapter, with explicit endpoint/group exceptions. Registering the
adapter is an explicit consumer choice; the template's default is not a compulsory policy
of the core library.

Keep native authentication and authorization: `[Authorize]`, `.RequireAuthorization()`,
named policies, roles, schemes and `IAuthorizationService` retain their ASP.NET Core meaning.
The actor adapter can make established actor context available to consumer authorization handlers.
Identified actor presence is not a substitute for authentication or permission checks.
There is no parallel HTTP actor authorization policy or actor-specific endpoint extension.
Native authorization decides whether a caller may execute the endpoint; the resolver maps
the effective authenticated principal to an application actor. An authenticated principal
that cannot be mapped must fail establishment rather than silently become anonymous.
The core's explicit actor guards remain useful for capability invariants and non-HTTP calls.

Honor native `[AllowAnonymous]`/`.AllowAnonymous()` without a second actor opt-out, and propose
separate tenantless endpoint metadata for the tenant exception. An anonymous tenant catalog
can still require a tenant; authenticated tenant creation can require an
actor without a tenant. Login/health endpoints can explicitly allow both. Allowing anonymous
access must not replace an authenticated caller's resolved actor with an anonymous actor.
Public names for tenantless metadata remain under review.

Enforce tenancy requirements separately from native authorization-policy selection. Native
`FallbackPolicy` is not combined with named/default policies, and `AllowAnonymous` bypasses
authorization enforcement. The template uses native fallback/default policies requiring
authentication; named permission policies explicitly include their intended authentication
requirements. The adapter does not impose an extra actor rule when native authorization
permits anonymous access. Putting tenancy only in a fallback policy would leave gaps.
The adapter's tenant default must survive both named permission policies and anonymous access
unless the endpoint explicitly permits tenantless execution.
See [native policy selection rules](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0).
`HttpContext.User` remains available for claims and native APIs; the accessor supplies the
canonical application actor or tenant from the respective accessor for capability calls. It does not replace
`ClaimsPrincipal` or require consumers to abandon native identity access.

The consumer supplies actor mapping and tenant resolution/admission. Actor establishment
maps a validated external identity to the application actor. Optional tenancy utilities may
extract a candidate from configurable route values or hostnames; other strategies, including
one tenant per user, can use consumer resolvers. Selection, canonical identity resolution and
admission are separate responsibilities. Resolve and admit a tenant before initializing its
context; using a path or hostname does not grant membership. Strategies and any composition
precedence are explicit consumer choices, not an automatic fallback chain.
An OIDC claim is not automatically an application UserId or an admitted tenant. Resolution
may perform asynchronous Access lookups; it is not limited to copying claims. Anonymous actor
and tenantless cases are deliberate, independent outputs. Tenant business data can load later;
the context's tenant identity is never appended or changed after initialization.

Routing and native authentication precede context establishment; required contexts must
exist before handlers and any authorization handlers that consume them. Prove the exact
middleware order and failure behavior with the selected native authentication schemes.
In particular, scheme-specific authorization must not leave context based on a different
principal. Native challenges must remain intact. Consumers choose admission/failure responses
and keep relevant requirements/permissions at module entry points for non-HTTP callers.

Build the adapters alongside trusted HTTP ingress in E3. Review their public interfaces and
prove defaults, endpoint/group precedence, anonymous and tenantless exceptions, named policies,
authentication schemes, denied admission, challenges and login/callback/health paths before
claiming support. Include unmapped authenticated principals, retained authenticated identity
on anonymous endpoints, and native policies that deliberately permit anonymous access.
Both cores remain package-free; worker/message consumers initialize their own operation scopes
directly. E1 currently includes no HTTP middleware.

[The reviewed E3.1 scope](plans/e3-1-http-actor-identity.md) specifies the actor-only adapter
and consumer recipe, checkpointed as `faefc0b`. It wraps the native evaluator after policy authentication,
with downstream completion for requests without a policy, so establishment precedes consumer
authorization handlers without capturing a different default-scheme identity.
[The report](reports/e3-1-http-actor-identity.md)
records actual cookie/policy/claim-action proofs and provider limitations; the architecture
direction is recorded in [ADR 0002](adr/0002-explicit-http-actor-establishment.md).

For the first tenancy HTTP slice, the owner confirmed on 2026-10-05 that resolution/admission
runs after native authorization and actor completion, before endpoint work. Its requirement
metadata is independent of native authorization policies. Native challenge/forbid paths
short-circuit before tenancy resolution. This bounded composition enforces admission during
resolution and supplies established tenant context to downstream capabilities, not native authorization
handlers; handlers needing tenancy require a later integration slice. This does not add
a second evaluator or a runtime dependency on ActorIdentity. The implementation was owner-reviewed
and checkpointed as `cfbac9a`, with route/subdomain utilities and an executable
Organization/Inventory consumer. Selection presets are explicit registration-time choices,
and an opt-in options utility supplies native subdomain host filtering patterns. Consumer
global exception handling uses native ProblemDetails.
[The E3.2 plan](plans/e3-2-http-tenancy.md) specifies its interface;
[the report](reports/e3-2-http-tenancy.md) records fresh guarantees and remaining limits.

Membership/admission receives a separate consumer-owned Access increment in E3. One application
user may belong to multiple Organizations; admission concerns the Organization selected for
the current operation. Organization is the sample domain/UI term and Tenant the technical
isolation boundary. Invitations, membership management and role policy receive their own
reviewable increments; neither foundation library acquires these domain rules.
[The approved E3.3 plan](plans/e3-3-persisted-access.md) defines persisted lookup/admission:
fresh active-membership admission per operation, with immutable already-admitted work and
an explicit public catalog exception for anonymous callers and mapped non-members.
[ADR 0003](adr/0003-access-registry-and-operation-admission.md) records the consumer registry
and admission-time revocation direction; [the report](reports/e3-3-persisted-access.md) records
implementation evidence and remaining limits.

OIDC/BFF registration starts as a small, editable sample/template helper in E3, composing
native `AddAuthentication`, `AddCookie` and `AddOpenIdConnect`. Provider settings, claim
mapping, session behavior, account linking and directory-gated/open admission stay visible
and consumer-owned. Promote focused authentication utilities only when exercised reuse
justifies a separate library. The later bootstrap CLI materializes the proven alternatives;
it does not turn authentication policy into a mandatory runtime layer. See
[native OIDC registration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-oidc-web-authentication?view=aspnetcore-10.0).

## Explicit control

For the initial EF proof, the owner confirmed one shared database, module-owned schemas and
tenant-discriminated tables. Ordinary tenant-scoped persistence rejects ownership changes;
any transfer is explicit consumer code. [E2.1](reports/e2-1-tenant-ownership.md) implements
explicit model registration and write validation with native ownership concurrency predicates,
proven for ordinary single-table string/GUID consumers on PostgreSQL and checkpointed as
`50e7933`. [E2.2](reports/e2-2-module-migrations.md) exercises consumer-owned native migrations
and history configuration for two module schemas without a new library mechanism,
checkpointed as `f2dcf2b`. [E2.3](reports/e2-3-tenant-relationships.md) adds an explicit native
same-tenant customer/address constraint as consumer-owned sample/template policy,
checkpointed as `d67c7fc`. [E2.4](reports/e2-4-versioned-profile-changes.md) adds an explicitly
advanced customer version and a two-save profile operation inside a caller-owned native
transaction, including stale-request, fault and cancellation proofs, checkpointed as
`6069c05`. The initial E2 scope is complete; E2.2 through E2.4 introduced no new reusable
mechanism. Shared cross-module transactions and the
remaining limits in [the E2 plan](plans/e2-persistence.md) require separate evidence.

Consumers register their DbContexts, entity mappings, migrations, handlers, transports,
queues, topics, subscriptions, and routes. Libraries may provide explicit registration
utilities and opt-in middleware whose effects and requirements are documented.

Use native EF transaction control where it suffices. A custom persistence scope must earn
its place by establishing a necessary protocol; document its entry, save, commit, disposal,
nesting, cancellation, and failure semantics. Saves and commits remain explicit. Middleware
does not silently commit, retry, compensate, or dispatch domain events.

Domain events are collected and processed deliberately. Integration contracts and their
mapping remain consumer-owned. Replay produces no historical external effects.

Supply callable delivery/maintenance operations separately from a simple optional hosted
worker. Consumers may host those operations themselves. Competing-consumer mechanisms need
their own claim, ordering, concurrency, and recovery evidence; leader election is excluded.

Library execution uses ordinary C# calls. Runtime code generation, a mediator, generic
repositories, and a generic workflow DSL are excluded. Existing archived EF migrations are
retained as evidence; native development-time EF migration scaffolding is allowed, with
generated migrations remaining consumer-owned and reviewed. Additional scaffolding choices
are stated in their slice.

## Dependencies and configuration

Start with explicit EF Core/PostgreSQL persistence and optional Rebus/RabbitMQ integration.
Critter Stack is a behavioral reference for messaging and persistence, not a selected runtime
dependency. Keep provider-specific capabilities available; a provider-neutral promise needs
evidence from actual alternatives rather than a lowest-common-denominator interface.

Library segments are independently adoptable except for documented dependencies. Event
sourcing does not require messaging; messaging does not require event sourcing; state-stored
modules do not depend on event-sourcing types or registrations. Technical libraries do not
reference sample module Contracts. Use project references during local development.

Use options for supported variations, and explicit registration or dependencies for larger
policy changes. Document how a setting changes a guarantee. Schema names, transport routes,
worker hosting, and application policy remain consumer choices.

Keep the email replacement seam narrow; SMTP/MailKit does not become a mandatory foundational
dependency. Authentication, general notifications, and a transport/provider matrix are not
automatically promoted into libraries.

## Template bootstrap direction

Provide a bootstrap CLI later to apply the template to a consumer repository. It will select
reviewed template alternatives and optional capabilities and materialize ordinary,
consumer-owned code and configuration. Intended choices include transport, event sourcing,
Aspire resources and authentication/admission setup such as Keycloak with directory-gated
or open registration. These are configuration goals, not currently supported alternatives.

Introduce an option after its implementation and composition have been exercised. Provider
selection does not require a common provider abstraction. Authentication/admission choices
configure consumer-owned Access policy. CLI implementation, existing-file handling and the
supported combinations remain later design decisions in E10; this adds no runtime dependency
or immediate CLI implementation work.

## Telemetry and tooling

Libraries use native `ILogger`, `ActivitySource`, and `Meter`. The host's ServiceDefaults
configures collection, export, and health. Aspire is local orchestration and topology-test
infrastructure, not a required library runtime.

Retain `.editorconfig`, CSharpier, Lefthook, and commitlint. Introduce a pinned pnpm workspace
and shared frontend subpackages with the first exercised frontend setup; existing npm-based
repository tooling remains until that deliberate transition. CI remains authoritative.

## Evidence and supported limits

Each library increment supplies tests through its public interface and real sample usage.
Transfer the intent of archived proofs, with independent expected outcomes, rather than
equating matching implementations with correctness. The same sample domain permits useful
comparisons without freezing its old implementation.

Shared database transactions, separate commits with compensation, and durable sagas are
different guarantees. Cross-module shared transactions still require a dedicated proof;
in-process orchestration alone provides no recovery after process death.

Historical identity, repair, replay-cost, retry, and shutdown limits remain visible until
new evidence resolves them. Production provider compatibility, packaging, deployment, and
recovery are separate proof gates, not consequences of local extraction.
