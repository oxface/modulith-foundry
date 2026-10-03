# Authorization and OpenFGA

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Executive conclusion

Do not put OpenFGA in the v1 reference architecture. The first ERP slice needs tenant membership, tenant-scoped roles, stable module permissions, an approval limit, separation-of-duties rules, and trusted workflow commands. Those are more clearly implemented with product-owned relational grants plus module-owned business policies than with a separately operated relationship graph.

This is a YAGNI decision, not a rejection of OpenFGA. OpenFGA is a credible Apache-2.0, CNCF-incubating system with relationship tuples, inherited permissions, contextual tuples, conditions, reverse queries, immutable model versions, PostgreSQL persistence, and a maintained .NET SDK. It becomes attractive if the product actually acquires resource sharing, nested groups, delegated relationships, permission inheritance through deep resource hierarchies, or a need to list objects/users through an authorization graph.

Keep a later adoption path by using stable principal/resource identifiers and permission names, concentrating coarse grants behind `Access.Contracts`, keeping resource policies in their owning modules, and testing authorization behavior as input/output examples. Do **not** add a generic provider abstraction or tuple-shaped application API before there is a second provider.

## Current versions, licenses, and support signals

| Component | Current stable release | License and compatibility | Support signal |
| --- | --- | --- | --- |
| OpenFGA server | `v1.21.0`, released 2026-09-20 | Apache-2.0 | CNCF Incubating since 2025-10-28; official repository reports production use and PostgreSQL 14+ support. The release is only three days old, so a deployment should pin a specifically tested patch instead of tracking `latest`. |
| `OpenFga.Sdk` for .NET | `0.10.4`, released 2026-07-20 | Apache-2.0; targets .NET 8 and .NET Standard 2.0, and NuGet computes compatibility with .NET 10 | Official SDK. Its documented test matrix currently names .NET 8/9 rather than an explicit .NET 10 CI job, although its support policy says it follows Microsoft's supported runtime lifecycle. Compatibility should therefore be verified in our own .NET 10 test before adoption. |

Primary sources:

- [OpenFGA server releases](https://github.com/openfga/openfga/releases)
- [OpenFGA server repository, license, storage support, and production statement](https://github.com/openfga/openfga)
- [CNCF OpenFGA project status](https://www.cncf.io/projects/openfga/)
- [`OpenFga.Sdk` 0.10.4 package metadata](https://www.nuget.org/packages/OpenFga.Sdk/0.10.4)
- [Official .NET SDK support policy and CI matrix](https://github.com/openfga/dotnet-sdk/blob/main/SUPPORTED_FRAMEWORKS.md)

OpenFGA has active releases, signed release artifacts, documented production operations, OpenTelemetry support, and known adopters. This is enough to treat it as a serious option. CNCF Incubation is a positive maturity signal, but it is not CNCF Graduation and does not remove the need to test our exact authorization model, SDK, availability, and upgrade behavior.

## What ASP.NET Core already supplies

ASP.NET Core 10 includes policy-based and resource-based authorization without another dependency:

- named policies composed of one or more requirements;
- `IAuthorizationRequirement` and handlers;
- `IAuthorizationService.AuthorizeAsync` for imperative checks against a loaded resource;
- endpoint metadata through `RequireAuthorization`; and
- custom/dynamic policy providers when policies cannot all be registered statically.

Resource authorization is deliberately imperative because endpoint attributes run before the application loads the resource. This is useful at the HTTP boundary, but HTTP authorization alone is insufficient for this system: the same application use case can be invoked in-process or by a Rebus workflow. The application handler must enforce the permission and business policy, while the API may perform an earlier coarse check for fast rejection.

Primary sources:

- [ASP.NET Core 10 policy-based authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0)
- [ASP.NET Core 10 resource-based authorization](https://learn.microsoft.com/en-us/aspnet/core/mvc/security/authorization/resource-based?view=aspnetcore-10.0)

## Recommended v1 authorization model

Authentication remains in Keycloak locally and Entra External ID in Azure. The JWT establishes identity only. The product owns authorization.

### Responsibility split

| Concern | Owner | Example |
| --- | --- | --- |
| Identity authentication | IdP | Prove the external `(issuer, subject)` identity. |
| Organization membership | Access module | User is an active member of organization `acme`. |
| System roles and coarse permission grants | Reviewed product code; assignments in Access | `sales-approver` grants `sales.orders.approve`. |
| Permission vocabulary and display metadata | Owning business module, exposed as a small manifest during composition | Sales defines `sales.orders.approve`; Access assigns it but does not invent its meaning. |
| Resource and business-state policy | Owning business module | Order is awaiting approval, actor did not submit it, amount is within authority, currency rule is satisfied. |
| Aggregate invariant | Owning aggregate/domain service | A cancelled order cannot be approved regardless of actor permissions. |
| Trusted workflow capability | Application composition and receiving module | The Order Fulfilment Process can invoke `ReleaseReservation`; browsers cannot. |
| HTTP enforcement | API plus application handler | The API requires authentication; the application handler remains authoritative. |

The Access module should persist tenant-scoped `Membership` and `MembershipRole` assignments using stable role codes. V1 role definitions and their permission bundles are a reviewed code catalog rather than tenant-editable rows. Permission IDs are stable text tokens, for example:

```text
sales.orders.view
sales.orders.submit
sales.orders.approve
inventory.stock.adjust
purchasing.orders.issue
```

The owning module defines the token and human description; the product-specific authorization composition assembles the manifests for the role-management UI. A system role is a built-in bundle of known permission tokens. Start without tenant-defined roles, role inheritance, explicit denies, field-level rules, or per-object grants because the example has no scenario requiring them.

An application command should make the policy visible:

```text
Approve sales order
  1. resolve authenticated human and organization membership
  2. require sales.orders.approve
  3. load the order
  4. evaluate Sales approval policy:
       - correct tenant
       - order is awaiting approval
       - approver is not the submitter
       - amount/currency is within this membership's authority
  5. invoke aggregate transition and commit audit/domain/outbox changes
```

Whether an approval authority record lives initially in Access or Sales is a domain-placement decision. The numeric/currency comparison and order-state semantics belong to Sales. Do not turn Access into a central service that imports every module's entities and business rules.

### Execution actors: v1 only

Implement only the actors required by actual flows:

- `HumanActor`: resolved from authenticated identity plus organization membership.
- `WorkflowActor`: created only by trusted internal message/application adapters for the Order Fulfilment Process.

Defer `SupportActor`, delegated administration, service-to-service credentials, impersonation, and privileged access management until a real support or extraction scenario exists. An HTTP request must never be able to select `WorkflowActor` through input data. Workflow commands still validate tenant, correlation, operation ID, ownership, and allowed state; they simply do not masquerade as user permissions.

### Query authorization

Avoid loading arbitrary rows and invoking an authorization check for every result. V1 list queries should be tenant-scoped and shaped by coarse permissions in SQL. Resource-specific checks apply when loading a particular object. Attachments inherit the owning business resource's access decision rather than receiving an independent public Blob permission model.

If the product later needs “show every object reachable through nested shares/groups,” that is an OpenFGA trigger rather than a reason to build a home-grown graph query engine.

### Failure and audit behavior

- Default deny when identity, tenant membership, permission, or required policy data cannot be resolved.
- Record security-significant denied actions with actor, tenant, operation, resource reference, reason code, and correlation ID; do not log secrets or full sensitive payloads.
- Return `404` instead of `403` where revealing another tenant's resource existence is itself sensitive.
- Treat role/permission changes as product writes with audit records.
- A concurrently revoked membership can race an already-authorized transaction. OpenFGA does not remove that race and may also serve cached decisions. V1 should document this boundary; only a named high-risk scenario should justify stronger transactional coupling.

## What OpenFGA provides

OpenFGA evaluates whether a typed user has a relation to a typed object under an immutable authorization model and stored relationship tuples. Its useful capabilities include:

- direct and computed relationships, including usersets and parent-to-child permission inheritance;
- conditions using typed request or persisted tuple context for some ABAC cases;
- contextual tuples that participate in a request without being persisted;
- `Check`/`BatchCheck`, `ListObjects`, and `ListUsers` queries;
- modular model source files that combine into one deployed model;
- immutable model versions that can be explicitly pinned during rollout; and
- stores that isolate models and tuples.

OpenFGA recommends one store for data that can participate in the same authorization result. For a multi-tenant SaaS, its own documented pattern is one store per environment with an `organization` type in the graph, not one store per customer. Store data cannot be related across stores. Models are immutable; a rename can require a new model, application change, tuple copy, and controlled cutover.

The default consistency mode may use caches when enabled. `HIGHER_CONSISTENCY` bypasses the cache and trades performance for freshness. An immediately checked new or revoked tuple can otherwise be stale. `ListObjects` and `ListUsers` also have configured deadlines and result limits (defaults documented as three seconds and 1,000 results), so they do not replace product SQL filtering, sorting, and pagination.

Primary sources:

- [OpenFGA concepts: models, stores, tuples, conditions, and contextual tuples](https://openfga.dev/docs/concepts)
- [Modular authorization models](https://openfga.dev/docs/modeling/modular-models)
- [Immutable model versions](https://openfga.dev/docs/getting-started/immutable-models)
- [Model migration guidance](https://openfga.dev/docs/modeling/migrating/migrating-models)
- [Query consistency modes](https://openfga.dev/docs/interacting/consistency)
- [Relationship-query behavior and ListObjects/ListUsers limits](https://openfga.dev/docs/interacting/relationship-queries)
- [OpenFGA multi-tenant SaaS pattern](https://openfga.dev/docs/use-cases/multi-tenant-saas)

## Why OpenFGA is not the v1 fit

### The initial problem is mostly RBAC plus business policy

The current example authorizes organization members through roles and coarse permissions. Its finer decisions use authoritative module state:

- approval amount and currency;
- order status;
- whether the actor submitted the order;
- reservation ownership and status;
- workflow correlation and operation idempotency.

OpenFGA conditions can compare typed context and therefore *can* express some amount or time predicates. That does not make it the right owner of approval policy. Currency conversion, consumed authority, separation of duties, product state, and the mutation being authorized live transactionally in module persistence. Passing all of that as check context duplicates product semantics and still leaves the module responsible for correctness.

Conditions have a 32 KiB persisted tuple-context limit, a 512 KiB overall request limit, and bounded CEL evaluation cost by default. More importantly, OpenFGA does not transact atomically with the module's EF Core write. It should decide graph reachability, not replace aggregate invariants or process state.

Primary source: [OpenFGA conditions and limits](https://openfga.dev/docs/modeling/conditions)

### It creates another authoritative dataset and failure boundary

If module relationships are copied into OpenFGA, the application needs an outbox-backed projection, bootstrap, replay, reconciliation, deletion handling, lag visibility, and a consistency policy. If grants live only in OpenFGA, the product still needs relational role metadata such as names and descriptions. OpenFGA's own source-of-truth guidance says entity hierarchies and searchable/filterable data generally belong in the application database; OpenFGA may own direct fine-grained grants or role membership.

Primary source: [OpenFGA source-of-truth guidance](https://openfga.dev/docs/best-practices/source-of-truth)

### It adds material operations and security work

Self-hosting requires an additional service, migrations, backups, monitoring, network policy, capacity planning, and a PostgreSQL datastore. The server supports PostgreSQL 14+, requires `openfga migrate` for initialization/upgrades, and its production guide recommends a database used exclusively by OpenFGA so it can be scaled without application contention. That recommendation can mean a separate database on the same server initially, but it is still separately owned persistence and connection capacity.

The server defaults to no authentication. Supported authentication modes are no authentication, pre-shared bearer keys, and OIDC. Production guidance requires authentication and TLS. OpenFGA's finer built-in authorization for access to stores/modules remains explicitly experimental and not recommended for production. Therefore, a self-hosted baseline would need a private endpoint plus a tightly held application credential; it should not be exposed directly to browsers or tenants.

Primary sources:

- [Server datastore and migration configuration](https://openfga.dev/docs/getting-started/setup-openfga/configure-openfga)
- [Production operations guidance](https://openfga.dev/docs/best-practices/running-in-production)
- [Authentication and TLS configuration](https://openfga.dev/docs/getting-started/setup-openfga/configure-openfga#configuring-authentication)
- [Experimental server access control](https://openfga.dev/docs/getting-started/setup-openfga/access-control)

### Lock-in is semantic more than legal

Apache-2.0 avoids commercial licensing lock-in and self-hosting keeps deployment control. The material lock-in comes from:

- model DSL and relation semantics;
- tuple/object naming and lifecycle;
- application dependence on `Check` and reverse-query behavior;
- synchronization and model migration procedures;
- latency, availability, and consistency assumptions; and
- using OpenFGA as the only source of fine-grained grants.

The emerging AuthZEN endpoint is not an escape hatch yet: OpenFGA labels it experimental, recommends its native API for normal OpenFGA integration, and AuthZEN does not cover writing/reading relationship data.

Primary source: [OpenFGA AuthZEN API status and limits](https://openfga.dev/docs/interacting/authzen)

## Explicit adoption triggers

Revisit OpenFGA when at least one real product requirement needs a relationship graph and the relational alternative has become materially harder, for example:

1. Users can share individual orders, documents, locations, or reports with named users, nested groups, or partner organizations.
2. Permissions inherit through multiple resource levels and exceptions are common, such as organization → division → warehouse → stock area → document.
3. Tenant-defined roles include nesting/delegation rather than merely bundling a known permission catalog.
4. The UI must answer “which objects can this user access?” or “which users can access this object?” from relationship data, not just tenant-scoped SQL predicates.
5. Several independently deployed services need one low-latency relationship decision system and are otherwise duplicating a graph model.
6. The team can name and operate the tuple source of truth, bootstrap/reconciliation process, availability target, fail-closed behavior, and model migration owner.

Do not adopt it merely because the product is multi-tenant, because permission checks are called “fine grained,” or because a future service extraction is possible.

## Migration path if a trigger appears

1. Keep current principal IDs, organization slugs/internal IDs, resource public references, and permission tokens stable.
2. Add OpenFGA model files and assertions to source control. Use module model files combined into one environment model; pin the deployed authorization model ID.
3. Choose one policy family with graph semantics, not all authorization at once.
4. Decide the source of truth explicitly. For existing relational relationships, initially treat OpenFGA as a derived projection.
5. Backfill tuples from a snapshot at a high-watermark, then consume ordered changes through the module outbox. Add deletion, replay, and reconciliation tests.
6. Run shadow checks against the existing policy and OpenFGA, recording mismatches without affecting users.
7. Test availability, timeouts, stale grant/revocation behavior, `HIGHER_CONSISTENCY`, model rollback, and ListObjects limits.
8. Cut over only the chosen graph policy behind a product-specific module interface. Keep module invariants and amount/status decisions local.
9. If OpenFGA later becomes authoritative for direct shares or role membership, perform an explicit one-way cutover with audit/export tooling; do not retain indefinite dual authority.

This path is enabled by ordinary good boundaries. It does not justify an `IAuthorizationProvider` switch, OpenFGA-shaped tuple types, or a home-grown policy DSL in v1.

## Proposed decision for the plan

**Baseline:** product-owned authorization using the Access module's relational membership/role grants, stable module-defined permission tokens, and module-owned resource/business policies. Enforce authorization in application handlers; use ASP.NET Core policies as an HTTP-boundary adapter. Implement only `HumanActor` and the trusted `WorkflowActor` needed by the first durable process.

**Deferred option:** OpenFGA, with current candidate versions `openfga/openfga` 1.21.0 and `OpenFga.Sdk` 0.10.4, subject to a fresh version/license/.NET 10 compatibility check when a trigger appears.

**Risk retained:** a future graph requirement will require tuple projection/backfill and policy migration. The migration plan above is cheaper and clearer than operating OpenFGA before the product contains a relationship-graph problem.
