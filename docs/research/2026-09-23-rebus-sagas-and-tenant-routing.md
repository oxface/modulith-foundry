# Rebus Sagas and Tenant Routing Research

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Scope and method

This note examines two open architecture questions:

1. whether long-running process managers should use Rebus' saga implementation or module-owned EF Core persistence; and
2. how an ASP.NET Core BFF should select an organization when one user can belong to several organizations.

Documented facts below come from official project source, official package metadata, Microsoft architecture guidance, an IETF specification, and OWASP guidance. Recommendations are explicitly labelled.

## Executive recommendation

- Use Rebus for transport, retries, RabbitMQ acknowledgements, and eventual Azure Service Bus portability, but do not initially use Rebus' `Saga<TSagaData>` persistence for the product workflow.
- Implement the first workflow as a concrete, module-owned durable process manager persisted through that module's EF Core `DbContext`. Commit inbox receipt, process state, domain changes, deadlines, audit entries, and outgoing outbox rows in one PostgreSQL transaction.
- Provide shared reliability plumbing for inbox, outbox, deadline claiming, and handler execution. Do not create a generic workflow language or a base class that tries to encode business states and transitions.
- Make the organization part of the canonical URL, for example `/o/{organizationKey}/orders/{orderId}`. Keep the BFF session for authentication tickets and an optional last-used organization only. Resolve and authorize the organization on every request.
- Support multi-organization membership from the beginning. Session-only tenant selection conflicts with independent tabs and bookmarks because the same browser session cookie is sent across matching requests.

## Rebus package baseline

### Documented facts

| Package | Current stable | License | Relevant role |
| --- | ---: | --- | --- |
| `Rebus` | `8.9.4` | MIT | Core bus, handlers, saga APIs, retry pipeline |
| `Rebus.ServiceProvider` | `10.7.2` | MIT | Generic Host / Microsoft DI integration through `IHostedService` |
| `Rebus.RabbitMq` | `10.1.1` | MIT | RabbitMQ transport |
| `Rebus.AzureServiceBus` | `10.7.1` | MIT | Azure Service Bus transport |
| `Rebus.PostgreSql` | `9.1.1` | MIT | PostgreSQL transport, sagas, subscriptions, timeouts, and outbox |

The core package was released on 2026-09-17. The PostgreSQL package's latest release is from 2024-05-20 and declares Rebus `>= 8.4.2` and Npgsql `>= 8.0.3`, rather than a narrow dependency on the current Rebus release.

Primary sources:

- [`Rebus` package metadata](https://www.nuget.org/packages/Rebus/8.9.4)
- [`Rebus.ServiceProvider` package metadata and hosted-service behavior](https://www.nuget.org/packages/Rebus.ServiceProvider/10.7.2)
- [`Rebus.RabbitMq` package metadata](https://www.nuget.org/packages/Rebus.RabbitMq/10.1.1)
- [`Rebus.AzureServiceBus` package metadata](https://www.nuget.org/packages/Rebus.AzureServiceBus/10.7.1)
- [`Rebus.PostgreSql` package metadata](https://www.nuget.org/packages/Rebus.PostgreSql/9.1.1)
- [Rebus MIT license](https://github.com/rebus-org/Rebus/blob/master/LICENSE.md)

### Recommendation

Pin these packages if Rebus is selected. Treat the older PostgreSQL integration and its broad dependency range as a verification risk. Rebus core and its transports remain suitable candidates; selecting them does not require selecting Rebus' saga or PostgreSQL outbox implementations.

## What a Rebus saga actually supplies

### Documented facts

Rebus describes `ISagaData` as the state of the state-machine instance implemented by a process manager. Saga data has an `Id` and a `Revision`, both managed by Rebus. A saga derives from `Saga<TSagaData>`, configures message-to-state correlation, and receives the matching data through its `Data` property. A message type can initiate a new instance through `IAmInitiatedBy<TMessage>`; subsequent message handlers correlate to existing state.

`MarkAsComplete()` causes persisted saga state to be deleted, or a never-persisted instance to be discarded. `MarkAsUnchanged()` suppresses persistence and revision advancement.

The `ISagaStorage` contract supports find, insert, update, and delete. Insert, update, and delete can throw `ConcurrencyException`; updates are revision-based optimistic concurrency. Rebus can invoke a saga's optional `ResolveConflict` override and limits conflict-resolution attempts. It also has an optional exclusive saga access pipeline, although optimistic concurrency still needs to be treated as a normal failure mode across workers and nodes.

Primary sources:

- [`Saga<TSagaData>` source: data, correlation, completion, and conflict resolution](https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/Saga.cs)
- [`ISagaData` source: state-machine role, ID, and revision](https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/ISagaData.cs)
- [`ISagaStorage` source: persistence and concurrency contract](https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/ISagaStorage.cs)
- [`LoadSagaDataStep` source: load, dispatch, insert/update/delete, and conflict loop](https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/LoadSagaDataStep.cs)
- [Rebus changelog entries for exclusive access and configurable conflict attempts](https://github.com/rebus-org/Rebus/blob/master/CHANGELOG.md)

Rebus also contains an idempotent-saga mode. Its source says an `IdempotentSaga<TSagaData>` records incoming message IDs and outgoing messages so externally visible sends can be preserved when a message is handled more than once.

Primary source: [`EnableIdempotentSagas` source and guarantees](https://github.com/rebus-org/Rebus/blob/master/Rebus/Sagas/Idempotent/IdempotentSagaConfigurationExtensions.cs)

### Durable timeouts

For transports without native deferred delivery, Rebus supports a timeout manager backed by durable timeout storage. The official SQL Server integration documentation demonstrates RabbitMQ plus a database-backed timeout manager: `bus.Defer(...)` sends the message to the timeout manager, which stores it until it becomes due and then forwards it. `Rebus.PostgreSql` advertises PostgreSQL timeout persistence as well.

Primary sources:

- [Official Rebus SQL Server timeout-manager configuration](https://github.com/rebus-org/Rebus.SqlServer#timeouts)
- [`Rebus.PostgreSql` supported persistence features](https://github.com/rebus-org/Rebus.PostgreSql)

### Important transactional boundary

A persistent Rebus saga is durable in the narrow sense that its process state survives process restarts. That does not by itself prove an atomic commit across:

- the Rebus saga-data store;
- a module's EF Core domain writes;
- inbox deduplication;
- outgoing messages; and
- scheduled deadlines.

Those stores must participate in one local database transaction, or every boundary must be made independently recoverable and idempotent. This is an architectural inference from the separate persistence contracts, not a claim that Rebus sagas are intrinsically unreliable.

The current `Rebus.PostgreSql` outbox has an open 2026 issue reporting that its transaction commit callback can execute before outgoing messages are saved when handling a message. That issue is specific to the PostgreSQL outbox implementation; it is not evidence of a defect in Rebus core saga persistence. It is nevertheless a reason not to base the template's atomicity story on this integration without a reproducing test and an upstream resolution.

Primary source: [`Rebus.PostgreSql` issue 55](https://github.com/rebus-org/Rebus.PostgreSql/issues/55)

## Rebus saga versus module-owned EF Core process manager

### Comparison

| Concern | Rebus `Saga<TSagaData>` | Module-owned EF Core process manager |
| --- | --- | --- |
| Persistence model | Generic Rebus saga store and correlation index | Explicit workflow table in the owning module schema |
| Durability | Yes, with persistent saga storage | Yes, with PostgreSQL |
| Concurrency | Revision-based optimistic concurrency; optional Rebus locking/conflict resolution | EF concurrency token and explicit retry/reload policy |
| Duplicate delivery | Optional idempotent saga mode | Module inbox keyed by consumer and message ID |
| Domain-state atomicity | Must be deliberately integrated with the module transaction | Natural when state and domain changes share the module `DbContext` |
| Outgoing-message atomicity | Requires verified Rebus persistence/outbox integration | Module outbox inserted by the same `DbContext` transaction |
| Deadlines | Rebus defer/timeout manager | Module deadline rows plus a native hosted worker |
| Module ownership | Rebus persistence types and lifecycle shape the workflow | Workflow state, migrations, queries, and audit remain module-owned |
| Extraction | Rebus-shaped saga can move with the endpoint | Module process manager moves with its schema; Rebus adapter remains at the edge |
| Testing | `SagaFixture` plus integration tests | Ordinary application tests plus PostgreSQL/RabbitMQ integration tests |

### Recommendation

Use a module-owned EF Core process manager for the first durable workflow. This aligns the critical state transition with the persistence model already chosen for modules and keeps the reliability invariant visible:

> For one received message, either the inbox receipt, process transition, domain changes, audit changes, deadline changes, and outgoing outbox rows all commit, or none commit.

Rebus should acknowledge the broker message only after that handler transaction succeeds. Redelivery remains possible, so the inbox check is required even if RabbitMQ publisher confirms and manual acknowledgements are enabled.

Do not reject Rebus sagas categorically. A focused spike can reconsider them if a workflow has no same-transaction domain writes or if a custom Rebus saga storage can demonstrably enlist in the module transaction without distorting module ownership. The proof must include process death at each commit boundary, duplicate delivery, concurrent correlated messages, timeout recovery, and poison-message behavior.

## How much saga framework should the template provide?

### Recommendation

Do not provide a generic `Saga<TState>` or declarative state-machine framework in the initial template. That would duplicate the hardest part of a workflow library while concealing the business-specific transitions.

Provide reusable infrastructure around concrete process managers:

- inbox records and a transaction wrapper that rejects an already-consumed `(consumer, messageId)`;
- outbox records and a background publisher with claiming, retry, backoff, and failure visibility;
- durable deadline records and a hosted worker that claims due work, advances or emits an outbox message atomically, and tolerates restarts;
- common message metadata: message ID, causation ID, correlation/process ID, tenant ID, occurred-at time, and contract version;
- an optimistic-concurrency convention and retry policy;
- operational queries for stuck, failed, waiting, and completed process instances;
- test helpers that stop at every failure boundary and redeliver the same message.

Each workflow should own a concrete EF entity and explicit transition methods. For example, `OrderFulfilmentProcess` can contain its current step, attempt counts, relevant business identifiers, pending deadline, and compensation status. Its handler remains application-layer code; it is not an aggregate and does not own another module's data.

If repetition appears after at least two real process managers, extract only proven mechanics. A small technical base containing identity, tenant, revision, timestamps, and completion metadata may then be justified, but business status and transition logic should remain concrete.

### Durable deadline algorithm

A minimal module-owned design is:

1. The message handler opens the module transaction and verifies/inserts the inbox receipt.
2. It loads the process row using tenant and correlation key, then applies one idempotent transition.
3. It inserts, supersedes, or cancels a deadline row as part of that same transaction.
4. It inserts resulting commands/events into the module outbox.
5. The transaction commits; only then does the Rebus handler return successfully.
6. A `BackgroundService` polls due deadlines. Multiple replicas claim rows with a PostgreSQL-safe competing-consumer pattern such as `FOR UPDATE SKIP LOCKED` inside short transactions.
7. Claiming a due deadline and inserting its outbox message happen atomically. The later process transition still passes through inbox/idempotency checks.

This is intentionally small, but it still needs integration tests for competing workers, crashes after claiming, crashes after commit but before publish, stale leases, duplicate deadlines, and cancellation races.

## Tenant selection options

### Documented facts

Microsoft distinguishes tenant mapping from authorization: identifying a tenant from a host, path, header, token, or cookie does not prove that the authenticated user may access it. Its multitenancy guidance explicitly supports tenant identifiers in URL paths, such as `https://app.contoso.com/tailwindtraders/`, and says the application must still authorize every request for the selected tenant.

Microsoft also recognizes that one user can belong to multiple tenants and describes two broad authorization models:

- an identity provider issues tenant-specific claims/tokens after tenant selection; or
- the IdP remains tenant-agnostic and the application looks up memberships and permissions.

The second model matches the decision that Keycloak/Entra supplies identity while product modules own authorization.

Primary sources:

- [Microsoft: map requests to tenants](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/considerations/map-requests)
- [Microsoft: identity and application-based authorization in multitenant solutions](https://learn.microsoft.com/en-us/azure/architecture/guide/multitenant/approaches/identity)
- [Microsoft: multitenant Web API URL, header, host, and token approaches](https://learn.microsoft.com/en-us/azure/architecture/best-practices/api-design#multitenant-web-apis)

OWASP classifies accepting an object identifier without checking the authenticated user's permission on that object as broken object-level authorization. Its guidance requires object-level authorization at every endpoint receiving an identifier and recommends automated tests for those controls. An organization key in a route is therefore a selector, never an authorization grant.

Primary source: [OWASP API1:2023 Broken Object Level Authorization](https://owasp.org/API-Security/editions/2023/en/0xa1-broken-object-level-authorization/)

HTTP cookies are returned according to host/domain/path scope, not browser-tab identity. A server-side session keyed by a normal BFF cookie is consequently shared by matching requests made from multiple tabs. This makes a single mutable `ActiveOrganizationId` in the session a poor authoritative selector when users expect two tabs to remain in two organizations.

Primary source: [IETF RFC 6265, HTTP State Management Mechanism](https://www.rfc-editor.org/rfc/rfc6265.html)

### Option comparison

| Selector | Bookmarks / deep links | Independent tabs | Operational cost | Main risk |
| --- | --- | --- | --- | --- |
| Session-only active organization | Poor | Poor: tabs share the cookie-backed session | Low | A tab switch silently changes the other tab's context |
| URL path | Strong | Strong | Low | Route identifier can be tampered with; authorization is mandatory |
| Subdomain | Strong | Strong | Medium/high | DNS, wildcard/custom certificates, forwarded host trust, cookie scope, and local development |
| Tenant claim in access token | Weak unless also represented in navigation | Usually requires token/session switching | Medium | Couples product tenancy to IdP token issuance and stale claims |
| Custom header | Poor for ordinary browser navigation | Client-dependent | Medium | Browser/proxy/caching mistakes and invisible context |

## Recommended BFF and URL model

Use a canonical route shape such as:

```text
/organizations                         organization chooser
/o/{organizationKey}/orders           organization-scoped UI
/o/{organizationKey}/orders/{orderId} deep link
/api/o/{organizationKey}/orders/...   organization-scoped BFF/API route
```

`organizationKey` should be a stable public identifier. A mutable display-name slug can be supported as an alias with a redirect, but it should not be the only durable bookmark key. The key is not a secret and does not need to encode the database ID.

Request handling should be:

1. Authenticate the BFF session and obtain the application user's stable identity.
2. Read `organizationKey` from the canonical route.
3. Resolve it to an internal `OrganizationId`.
4. Verify a current membership and the product permission required for this operation.
5. Create an immutable request-scoped `TenantContext` only after that verification.
6. Pass the verified tenant ID into the module capability; do not let persistence code read an unvalidated route value.
7. Constrain every object lookup by both tenant ID and object ID, and test cross-tenant key substitution.

The server-side session may store `LastUsedOrganizationId` only to improve navigation. Visiting `/` may redirect to that organization when membership is still valid; otherwise it shows the chooser. Explicit organization routes always win. A write endpoint must never infer its tenant solely from the mutable last-used session value.

Return a consistent not-found or forbidden policy for unauthorized tenant/object combinations to avoid unnecessary existence disclosure. Ensure cache keys include the verified tenant ID; Microsoft specifically warns that caches which omit header-selected tenancy can leak another tenant's response.

### Why not subdomains first?

Subdomains are a valid later option for branding or custom domains, but they do not strengthen authorization by themselves. They add host preservation and forwarded-header trust, DNS, certificates, cookie-domain decisions, local development, and OIDC redirect-URI management. Microsoft explicitly requires authorization even when a custom domain maps the request to a tenant.

For the first product slice, path tenancy supplies bookmarks and tab isolation without those costs. The internal resolver can accept a future trusted host-derived key as another input without changing the verified `TenantContext` or module contracts.

## Required proof tests

### Durable process manager

- A crash before commit causes RabbitMQ redelivery and no visible process/domain/outbox change.
- A crash after database commit but before broker acknowledgement causes redelivery, which the inbox treats as already consumed.
- Two messages for one process arriving concurrently cause one valid serialized outcome, not lost updates.
- A duplicate initiating message creates one process instance.
- A waiting process and its deadline survive full application and broker restarts.
- Two deadline workers cannot execute one deadline twice as a business action.
- Publishing failure leaves the outbox row retryable.
- Compensation is itself idempotent and auditable.

### Tenant routing and authorization

- Two tabs can remain on different organizations without changing each other's effective tenant.
- Refresh and bookmarked deep links retain the organization.
- Replacing `organizationKey`, `orderId`, or both with another tenant's identifiers never exposes or mutates data.
- Removing a membership invalidates access even when the session still remembers that organization.
- Cache entries, rate-limit keys, audit records, inbox rows, outbox messages, and process correlation all include the verified tenant ID where applicable.
- Route-selected tenancy and a message/body tenant value cannot conflict silently; one canonical verified context is used.

## Decision boundary

This research supports, but does not itself decide, the following proposals:

1. Rebus is the transport adapter, not the owner of product workflow state.
2. The first durable workflow is a module-owned EF Core process manager with same-transaction inbox/outbox/deadlines.
3. Shared code supplies reliability mechanisms, not a general saga framework.
4. Canonical URL-path tenancy is authoritative for browser navigation; session tenancy is only a remembered preference.
5. A request route selects an organization, but only application authorization establishes the tenant context.
