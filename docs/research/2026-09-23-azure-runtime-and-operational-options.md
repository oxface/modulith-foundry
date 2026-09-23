# Azure Runtime and Operational Options

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Purpose and method

This note researches the architecture questions raised after the initial technology baseline. It uses primary sources only: Microsoft/Azure and Aspire documentation and pricing APIs, official project documentation/repositories, official NuGet metadata, and license texts. It does not authorize application scaffolding.

Prices are especially time- and region-sensitive. The examples below use public pay-as-you-go retail rates in USD for **East US**, 730 hours/month, no negotiated discounts or reservations, queried on the date above. They are planning ranges, not quotes. Microsoft's calculator draws unit prices from the same Retail Prices API and can show negotiated agreement rates after sign-in; create and save a calculator estimate before any Azure deployment ([Pricing Calculator documentation](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/pricing-calculator), [Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices)).

## Executive implications

- **Proposed:** Treat Azure as the likely production cloud and AKS as a provisional deployment choice, not yet an application constraint. A modular monolith with one public process does not need Kubernetes for application architecture; AKS must earn its cost and operational surface through the desired GitOps, isolation, scaling, or organizational model.
- **Proposed:** If AKS is selected, use Azure Front Door Premium with WAF/bot protection at the global edge, an internal AKS load balancer through Private Link, and the AKS-managed Gateway API implementation for in-cluster routing. Do not start a new deployment on ingress-nginx this close to its support deadline.
- **Proposed:** Use Keycloak locally only as an identity issuer. Persist all tenant membership, roles, permissions, delegation, and business authorization in the product. For production, choose Entra B2B for invited partner workforces or an Entra External ID external tenant for customer/consumer identities; do not target legacy Azure AD B2C for a new customer.
- **Proposed:** Use RabbitMQ for deterministic local acknowledgement/redelivery/dead-letter failure tests and keep a small compatibility suite against the Azure Service Bus emulator. Before release to Azure, run a still smaller suite against a temporary real Service Bus namespace because the emulator intentionally omits important cloud behavior.
- **Proposed:** Rebus is justified if one handler model must run on RabbitMQ locally and Azure Service Bus in production. `Rebus.ServiceProvider` already hosts the bus as an `IHostedService`; custom `BackgroundService` code is appropriate for a repository-owned outbox relay, not for rebuilding Rebus's receive pump.
- **Proposed:** Keep three separate BFF concerns explicit: cookie, server-side authentication ticket, and Data Protection key ring. Redis may store tickets; it should not be the durable production Data Protection key store. Persist Data Protection keys to Blob Storage and encrypt them with a versionless Key Vault key.
- **Proposed:** Use a conventional C# AppHost project. A single-file `apphost.cs` avoids TypeScript-host lifecycle issues but currently cannot be tested with `DistributedApplicationTestingBuilder`, which conflicts with the stated whole-topology testing goal.
- **Proposed:** Keep Apache-2.0 for this template. It is permissive, compatible with proprietary derived products, and adds an express patent grant. Derived distributions still need to preserve applicable notices.
- **Risk:** Azure budgets alert; they do not stop consumption. Protect spend with layered request controls, bounded autoscaling and telemetry, SKU policies, quotas, and operational kill switches.

## 1. Azure deployment shape, gateway, and cost

### Likely production topology

For a single-region first production deployment:

```text
Internet
  -> Azure Front Door Premium + WAF/bot/rate-limit rules
  -> Private Link
  -> internal Standard Load Balancer
  -> AKS-managed Gateway API implementation
  -> 2+ replicas of the single application image

Application
  -> Azure Database for PostgreSQL Flexible Server
  -> Azure Managed Redis (only for proven ticket/cache use)
  -> Azure Service Bus Standard
  -> Key Vault via Workload Identity
  -> Blob Storage for the Data Protection key ring
  -> Azure Monitor / Application Insights with controlled ingestion
```

Front Door Premium can connect privately to an AKS internal load balancer, avoiding a publicly reachable origin ([Front Door Private Link origins](https://learn.microsoft.com/en-us/azure/frontdoor/private-link)). Premium includes WAF, bot protection, and Private Link in its base tier, whereas Standard is cheaper but does not include the managed WAF rule set ([Front Door pricing](https://azure.microsoft.com/en-us/pricing/details/frontdoor/), [WAF tuning](https://learn.microsoft.com/en-us/azure/web-application-firewall/afds/waf-front-door-tuning)).

For in-cluster ingress, prefer the AKS application-routing Gateway API implementation. AKS now documents Gateway API as the long-term path; upstream ingress-nginx maintenance ended in March 2026 and Microsoft's critical-patch support for its managed NGINX add-on ends in November 2026 ([AKS application routing notice](https://learn.microsoft.com/en-us/azure/aks/app-routing), [Gateway API implementation](https://learn.microsoft.com/en-us/azure/aks/app-routing-migration)). Application Gateway for Containers is a valid managed alternative when its specific L7 features justify another service, but it is not automatically required for one application route ([Application Gateway for Containers overview](https://learn.microsoft.com/azure/application-gateway/for-containers/overview)). Likewise, API Management is not justified until there is an external API product or policy surface.

AKS production reliability guidance recommends multiple availability zones, at least two application replicas, a Standard Load Balancer, and at least two nodes in a system pool. Those requirements, not the control plane alone, create the real floor under AKS cost ([AKS reliability guidance](https://learn.microsoft.com/en-us/azure/aks/best-practices-app-cluster-reliability)).

### Current unit-rate anchors

The following are selected East US public retail-rate anchors, not a complete bill:

| Item | Example current unit rate | Approximate monthly anchor | Important qualifier |
| --- | ---: | ---: | --- |
| AKS Standard control plane | $0.10/hour | $73 | The Free tier has no control-plane SLA; nodes still cost money. |
| Three Linux `D2as_v5` nodes | $0.086/node-hour | $188 | Before OS disks, load balancer, public/private networking, backup, and overprovisioning. |
| PostgreSQL Flexible Server, general-purpose 2 vCore | $0.178/hour | $130 compute | 128 GiB Premium SSD adds about $15. Zone-redundant HA bills primary and secondary compute and storage, roughly doubling this part. |
| Azure Managed Redis Balanced B0 | $0.016/node-hour | $12 one node / $23 two nodes | Suitable only after ticket/cache capacity and availability have been validated. |
| Service Bus Standard | $10/month base | $10 at low volume | First 13 million messaging operations/month are included; Premium is about $0.9275/MU-hour, roughly $677/month for one MU. |
| Container Registry Basic | $0.1666/day | $5 | Includes 10 GB; networking and excess storage are separate. |
| Front Door Standard / Premium | $35 / $330 base | $35 / $330 plus requests and transfer | Premium includes WAF and Private Link. |
| Key Vault standard operations | $0.03/10,000 | Usually cents | HSM keys, renewals, and Managed HSM have different meters. |
| Log Analytics Analytics Logs | $2.30/GB ingested | 25-100 GB is about $58-$230 before applicable free benefit | Ingestion and retention are commonly the largest Azure Monitor costs. |

Sources: [AKS pricing](https://azure.microsoft.com/en-us/pricing/details/kubernetes-service/), [PostgreSQL pricing](https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/), [Azure Managed Redis pricing](https://azure.microsoft.com/en-us/pricing/details/managed-redis/), [Service Bus pricing](https://azure.microsoft.com/en-us/pricing/details/service-bus/), [Container Registry pricing](https://azure.microsoft.com/en-us/pricing/details/container-registry/), [Front Door pricing](https://azure.microsoft.com/en-us/pricing/details/frontdoor/), [Key Vault pricing](https://azure.microsoft.com/en-us/pricing/details/key-vault/), and [Azure Monitor cost model](https://learn.microsoft.com/en-us/azure/azure-monitor/logs/cost-logs).

Do not start a new deployment on Azure Cache for Redis. Basic, Standard, and Premium tiers retire on 2028-09-30, and Microsoft recommends Azure Managed Redis now ([retirement FAQ](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/retirement-faq)).

### Example monthly bands

These ranges deliberately include uncertainty for disks, load balancing, DNS, backup, log volume, request volume, and egress:

| Shape | Assumptions | Planning band/month |
| --- | --- | ---: |
| Paid pilot / staging | AKS Standard; two small nodes; non-HA PostgreSQL; one-node Redis if needed; Service Bus Standard; ACR Basic; Front Door Standard; modest logs | **$450-$750** |
| Single-region production baseline | AKS Standard; three small nodes across zones; 2+ app replicas; PostgreSQL zone-redundant HA; two-node Redis; Service Bus Standard; ACR; Front Door Premium/WAF/Private Link; controlled logs | **$900-$1,600** |
| Hardened / growth baseline | Larger or separate node pools, more database headroom, high telemetry/egress, Application Gateway for Containers or DDoS IP protection, and possibly Service Bus Premium | **$1,800-$4,000+** |

These bands exclude support plans, Entra licenses/MAU charges, outbound email, domain registration, cross-region disaster recovery, security products such as Defender/Sentinel, and engineering/on-call cost. Reservations and savings plans can lower steady-state compute/database rates, but should follow measured utilization rather than precede it.

**Challenge:** for one modest web process, managed PostgreSQL and the secure edge can cost as much as or more than the application compute. AKS also creates upgrade, networking, policy, identity, backup, and incident-response duties. Keep Azure Container Apps or App Service as an explicit comparison at the production-target ADR even if AKS remains the preferred learning target.

## 2. Rate limiting and spend blast radius

No single rate limiter protects against 500 million requests. Use layers with different identities and failure modes:

1. **Global edge:** Front Door Premium WAF managed rules and Bot Manager, geo/ASN/client-fingerprint blocks where justified, path-specific fixed-window rules, and size/method constraints. Front Door rate limiting is per socket IP over one- or five-minute windows; counters can lag across edge servers and very low thresholds may let excess requests through. It is not a global tenant quota ([Front Door WAF rate limiting](https://learn.microsoft.com/en-us/azure/web-application-firewall/afds/waf-front-door-rate-limit), [application DDoS guidance](https://learn.microsoft.com/en-us/azure/web-application-firewall/application-ddos-protection), [custom-rule matches](https://learn.microsoft.com/en-us/azure/web-application-firewall/afds/waf-front-door-custom-rules)).
2. **Application edge:** ASP.NET Core partitioned limits by anonymous fingerprint/IP before authentication and then by immutable subject, tenant, client application, and endpoint cost. Combine time-based and concurrency limits; expensive exports/searches/writes receive much tighter limits than cheap cached reads. The built-in limiters are process-local unless backed by an explicitly designed distributed policy, so per-replica limits must account for maximum replica count ([ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)).
3. **Business quotas:** enforce product-owned daily/monthly quotas for expensive actions, imports, exports, email, AI, and third-party calls. A valid authenticated identity is not permission to create unbounded spend.
4. **Backpressure:** bound request bodies, concurrency, database connection pools and statement timeouts, outbox batch size, consumer concurrency, queue size/TTL, retry count, and retry delay. Rejections should produce sampled/aggregated telemetry rather than one expensive log record per malicious request.
5. **Scaling ceilings:** set HPA maximum replicas and AKS cluster-autoscaler maximum nodes. The node maximum bounds autoscaler-driven scale-up, although it does not undo manual/external scaling beyond that value ([AKS cluster autoscaler](https://learn.microsoft.com/en-us/azure/aks/cluster-autoscaler-overview)).
6. **Cloud governance:** isolate production in its own subscription, restrict regions/resource types and allowed VM SKUs with deny policies, use conservative service quotas, and require review for capacity/SKU increases. Azure has a built-in `Allowed virtual machine size SKUs` deny policy ([Azure VM policy reference](https://learn.microsoft.com/en-us/azure/virtual-machines/policy-reference)).
7. **Detection and response:** actual and forecast budgets at subscription/resource-group scope, anomaly alerts, traffic/cost dashboards, and an action group with a rehearsed runbook to tighten WAF rules, cap consumers, or disable nonessential costly features. Budgets do **not** stop resources or consumption, and cost evaluation is delayed; they are an alarm, not a circuit breaker ([budget behavior](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/tutorial-acm-create-budgets), [cost alerts](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/cost-mgt-alerts-monitor-usage-spending)).

At current Zone 1 rates, 500 million Front Door Premium requests alone are approximately $750 in request charges (`500,000,000 / 10,000 * $0.015`), in addition to the $330 base and data transfer. The much larger danger is letting those requests reach autoscaling compute, database, downstream APIs, or high-volume logs. Edge rejection plus hard capacity ceilings limits that multiplication.

All Azure public IPs receive infrastructure-level DDoS protection. DDoS IP Protection adds per-IP features; DDoS Network Protection adds rapid response, WAF discounts, and documented attack cost protection. For a small estate, Microsoft's own comparison says per-IP protection is generally more economical, but only Network Protection includes the cost-protection benefit ([tier comparison](https://learn.microsoft.com/en-us/azure/ddos-protection/ddos-protection-sku-comparison), [pricing comparison](https://learn.microsoft.com/en-us/azure/ddos-protection/ddos-pricing-guide)). Front Door/WAF is the first priority for this HTTP application; buy additional DDoS coverage from a threat/cost analysis, not as a ritual checkbox.

## 3. Brokers, acknowledgements, and Rebus

### Azure Service Bus emulator

The emulator supports AMQP-over-TCP development/testing of preconfigured queues, topics, subscriptions, filters, dead-lettering, and local logs. It is intentionally not a local Azure clone: there is one namespace, entities must be configured before startup, data disappears on restart, and it omits runtime management, Entra authentication, partitioned entities, AMQP WebSockets, networking, autoscale, geo-DR, large messages, portal metrics, and alerts. Documented limits include 50 entities, 50 subscriptions per topic, 10 concurrent namespace connections, 256 KB messages, 100 MB entities, and a one-hour maximum TTL ([emulator overview and limitations](https://learn.microsoft.com/azure/service-bus-messaging/overview-emulator)).

The installer repository is MIT, but the emulator binary has a separate Microsoft EULA allowing internal development/test use and prohibiting production use, redistribution, and standalone hosted use. Reference the Microsoft Container Registry image; do not vendor or mirror it ([installer repository](https://github.com/Azure/azure-service-bus-emulator-installer), [emulator EULA](https://github.com/Azure/azure-service-bus-emulator-installer/blob/main/EMULATOR_EULA.txt)).

Use it for Azure transport compatibility. Keep a scheduled/pre-release test against a temporary real namespace for lock loss, sessions if used, networking/authentication, and other cloud-only behavior.

### RabbitMQ failure semantics

RabbitMQ is the stronger deterministic local broker for the desired crash/ack tests:

- With manual acknowledgements, the broker removes a delivery only after positive acknowledgement. Closing the consumer channel/connection with a delivery unacknowledged automatically requeues it; therefore every handler must be idempotent.
- `basic.reject`/`basic.nack` with `requeue=true` can create an immediate retry loop. With `requeue=false`, the message reaches a configured dead-letter exchange or is discarded.
- Publisher confirms and consumer acknowledgements are independent. A publish confirm means the broker/queues accepted responsibility, not that a consumer completed business work.
- Quorum queues are the replicated, data-safety-oriented choice. Their poison-message delivery limits help bound redelivery.
- Dead-lettering is at-most-once by default. Quorum queues require explicit at-least-once dead-letter configuration (`dead-letter-strategy=at-least-once`, `overflow=reject-publish`, DLX, and stream feature), and duplicates remain possible at the target.

Sources: [RabbitMQ acknowledgements and confirms](https://www.rabbitmq.com/docs/confirms), [quorum queues and poison handling](https://www.rabbitmq.com/docs/quorum-queues). RabbitMQ 4.3.6 was current on 2026-09-16; the server/tier-one plugins are MPL-2.0 ([downloads](https://www.rabbitmq.com/docs/download), [license](https://github.com/rabbitmq/rabbitmq-server/blob/main/LICENSE)). Its short support windows imply an active upgrade cadence.

Required broker failure tests:

1. Kill a consumer after its database commit but before acknowledgement; prove redelivery and inbox idempotency.
2. Kill it before commit; prove rollback and redelivery.
3. Drop the broker connection while a handler is running.
4. Exhaust bounded retries/delivery count; inspect the dead-letter/error message and diagnostics.
5. Publish to an unroutable destination; prove the failure is observable rather than silently “confirmed.”
6. Restart the broker; prove durable/quorum queues and persistent messages recover.

### Rebus versus native hosted services

Current stable packages are independently versioned and MIT licensed:

| Package | Current stable | Use |
| --- | ---: | --- |
| [`Rebus`](https://www.nuget.org/packages/Rebus) | 8.9.4 | Core bus API |
| [`Rebus.ServiceProvider`](https://www.nuget.org/packages/Rebus.ServiceProvider) | 10.7.2 | .NET DI and hosted lifecycle |
| [`Rebus.RabbitMq`](https://www.nuget.org/packages/Rebus.RabbitMq) | 10.1.1 | RabbitMQ transport |
| [`Rebus.AzureServiceBus`](https://www.nuget.org/packages/Rebus.AzureServiceBus) | 10.7.1 | Azure Service Bus transport; requires Standard because it uses topics |
| [`Rebus.PostgreSql`](https://www.nuget.org/packages/Rebus.PostgreSql) | 9.1.1 | PostgreSQL transport/storage; not the proposed outbox baseline |

`Rebus.ServiceProvider` already starts and stops Rebus through `IHostedService`; consumers do not need a hand-written broker-polling `BackgroundService` ([official repository](https://github.com/rebus-org/Rebus.ServiceProvider)). A native hosted worker can use `RabbitMQ.Client` or `Azure.Messaging.ServiceBus` directly and can absolutely exercise real broker acknowledgements, but `BackgroundService` itself supplies no acknowledgement, retry, routing, error-queue, or transport abstraction. Use that lower-level path only if the application deliberately owns those policies.

**Proposal:** if production is Azure Service Bus but deterministic local broker tests use RabbitMQ, Rebus earns its place by keeping the handler model stable across transports. Keep all Rebus types inside module implementations/infrastructure. The outbox row should be an application integration-event envelope, not a serialized Rebus transport object.

`Rebus.PostgreSql` still has an open May 2026 issue reporting a path that commits the database transaction before persisting outgoing messages on current .NET/Npgsql ([issue #55](https://github.com/rebus-org/Rebus.PostgreSql/issues/55)). Keep the repository-owned EF/PostgreSQL outbox and a native hosted relay unless that failure is reproduced and resolved. Rebus transport delivery begins after the relay claims a committed outbox row; consumer-side inbox and business effects commit before the broker acknowledgement.

## 4. BFF cookies, tickets, Data Protection, and secrets

### Separate the state stores

1. The browser carries an HttpOnly, Secure, same-site-aware authentication cookie.
2. ASP.NET Core `ITicketStore` can move the full authentication ticket server-side so the cookie contains only an opaque identifier. ASP.NET Core defines the interface but does not ship an official Redis implementation ([`ITicketStore`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore?view=aspnetcore-10.0)).
3. The ASP.NET Core Data Protection key ring encrypts/signs cookies and other protected payloads. `PersistKeysToStackExchangeRedis` stores this key ring; it does not implement the ticket store.

Redis is defensible for distributed tickets when the BFF runs multiple replicas and requires server-side revocation. The custom adapter must define serialization/versioning, key namespacing, absolute/sliding expiry, renewal, explicit revocation, concurrent updates, and behavior during Redis failure. Decide whether loss of Redis logs everyone out (usually acceptable) and whether Redis unavailability fails login/request closed.

Do not make Redis the durable production Data Protection key store. Microsoft warns that Redis does not persist by default and key loss invalidates protected data. Persist the key ring in Azure Blob Storage and protect it with a **versionless** Key Vault key so key rotation remains usable; set a stable application name across replicas ([Data Protection key providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers?view=aspnetcore-10.0), [Data Protection configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview)). Access Blob, Key Vault, Redis, PostgreSQL, and Service Bus through AKS Workload Identity/managed identities wherever supported, not embedded client secrets ([AKS Workload Identity and Key Vault CSI](https://learn.microsoft.com/en-us/azure/aks/csi-secrets-store-identity-access)).

### Local and production secrets

- **Local developer:** Aspire secret parameters stored through `aspire secret`/.NET Secret Manager, or disposable generated container passwords. Secret Manager is development-only and its contents are not encrypted. Never commit realm admin credentials, database passwords, or client secrets ([Aspire external parameters](https://aspire.dev/fundamentals/external-parameters/), [`aspire secret`](https://aspire.dev/reference/cli/commands/aspire-secret/), [.NET development secrets](https://learn.microsoft.com/en-gb/aspnet/core/security/app-secrets?view=aspnetcore-10.0)).
- **CI:** short-lived workload federation where possible; otherwise CI-native masked secrets scoped to the environment. Do not require CI to read a developer's local secret store.
- **Azure:** Key Vault with RBAC, private access where warranted, soft-delete/purge protection, audit logs, and workload identity. Prefer references/SDK caching over reading Key Vault on every request.
- **Configuration boundary:** identifiers, URLs, feature settings, and rate limits are ordinary configuration. Only credentials, encryption material, and comparably sensitive values belong in the secret store.

## 5. Identity-only IdP and production Entra choice

The suggested boundary is sound: the IdP authenticates; the product authorizes.

Configure Keycloak so the access token supplies only identity/protocol claims needed by the API. Its default client scopes can map realm/client roles, so disable broad role scopes and omit product-role mappers rather than assuming Keycloak is identity-only by default ([Keycloak administration guide](https://www.keycloak.org/docs/latest/server_admin/)). Map an external principal to a product user by immutable issuer plus subject. Email is mutable and must not be the durable key.

Store tenant membership, business roles/permissions, delegation, approval limits, and authorization policy in product-owned module tables. A token establishes “who”; module application/domain policy decides “may do what to which tenant/resource now.” For Entra, Microsoft recommends immutable tenant/object identifiers for tenant-scoped data and validates audience, tenant, subject, and actor ([Microsoft identity claims validation](https://learn.microsoft.com/en-us/entra/identity-platform/claims-validation)).

The production options represent different products:

- **Entra B2B collaboration:** a workforce tenant invites external partners/guests. This fits an ERP sold into organizations whose users are managed or invited by those organizations.
- **Entra External ID external tenant:** CIAM for customer-facing/self-service identities.
- **Azure AD B2C:** legacy direction; it stopped being available to new customers on 2025-05-01. Do not base a new product plan on buying it.

Sources: [External ID/B2B overview](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview), [Azure AD B2C availability notice](https://learn.microsoft.com/en-us/azure/active-directory-b2c/faq).

**Open decision:** Is this ERP primarily sold to organizations that invite their employees/partners, or directly to self-registering customers? This determines B2B versus an External ID external tenant and also shapes tenant provisioning, support, and pricing.

## 6. Testing, AppHost, pnpm, k3s, and Flux

### Testing layers

Aspire testing starts the AppHost topology and resources as separate processes. It is closed-box integration/E2E testing rather than a place for direct dependency-injection substitution ([Aspire testing overview](https://aspire.dev/testing/overview/)). Use distinct layers:

- **Domain unit tests:** pure aggregate/value-object/decider behavior.
- **Application tests:** multiple application/domain classes with no network, filesystem, database, clock, or broker I/O; these are useful and should be named explicitly.
- **Infrastructure integration tests:** Testcontainers for PostgreSQL, RabbitMQ, Redis where justified, and failure injection. These should substantially outnumber mocked unit tests around persistence/messaging behavior.
- **API integration tests:** `WebApplicationFactory` for routing, authentication/authorization plumbing, problem details, and selected infrastructure replacements.
- **Aspire topology tests:** a small number of complete HTTP/broker workflows proving the real resource graph starts, becomes healthy, and collaborates correctly.
- **Playwright:** defer until a real browser UI has login/navigation/user journeys worth protecting. HTTP tests are enough for the template's minimal exercise frontend/API boundary.

Although Aspire supports a single-file C# `apphost.cs`, official testing documentation states that file-based AppHosts cannot be used with `DistributedApplicationTestingBuilder` because there is no project reference/generated `Projects.*` type ([advanced testing limitations](https://aspire.dev/testing/advanced-scenarios/)). **Recommendation: use a conventional C# AppHost project**, not TypeScript and not a single-file host, because whole-topology tests are a stronger requirement than file-count reduction.

Use pnpm and commit its lockfile for any frontend. Aspire 13's unified JavaScript resource has `WithPnpm()` and publishing uses `pnpm install --frozen-lockfile`; the C# AppHost itself does not require npm ([Aspire JavaScript model](https://learn.microsoft.com/dotnet/aspire/whats-new/dotnet-aspire-9), [Aspire prerequisites](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/setup-tooling)). Do not add a second npm lockfile merely for Aspire.

### k3s and Flux proof

A VM-hosted k3s slice is useful for verifying OCI image behavior, Kubernetes resources, probes, config/secret mounting, resource limits, migration jobs, rollback, and Flux reconciliation. It does **not** validate AKS networking, availability zones, managed identities, Private Link, Azure load balancers, Azure CSI behavior, or production storage.

k3s bundles Traefik, ServiceLB, local-path storage, CoreDNS, and metrics-server. Traefik/ServiceLB normally take ports 80/443 on nodes; explicitly disable or account for them before testing another gateway. Local-path storage binds data to one node and is not a model for managed Azure storage ([k3s networking services](https://docs.k3s.io/networking/networking-services), [packaged components](https://docs.k3s.io/installation/packaged-components), [local storage](https://docs.k3s.io/add-ons/storage)).

Flux 2.9.5 was current on 2026-08-31 and is Apache-2.0. Bootstrap installs controllers and commits/pushes self-management manifests, so it needs a reachable Git remote and tightly scoped credentials. `flux install` can test controllers without Git but does not prove GitOps. Uninstalling Flux does not remove reconciled workloads ([Flux installation](https://fluxcd.io/flux/installation/), [Flux releases](https://github.com/fluxcd/flux2/releases)). Keep environment-specific storage classes, identities, gateway classes, and endpoints in overlays; require a real AKS validation environment before calling deployment proven.

## 7. Patterns to borrow from Marten without adopting it

Marten is not proposed as a dependency, but its documented PostgreSQL event-store model is a useful design reference.

- **Event/stream storage:** one append-only events table with global sequence, event ID, stream ID, stream version, JSONB payload, stable event-type alias, timestamp, tenant, and CLR-type diagnostic metadata; a separate streams table owns stream metadata/version; a progression table checkpoints asynchronous projections. This supports unique event IDs, expected-version concurrency, per-stream ordering, and global projection ordering ([Marten event-store schema](https://martendb.io/events/storage)). Our implementation should avoid persisting assembly-qualified CLR type names as its canonical discriminator; store a deliberately registered stable alias plus explicit schema version.
- **Evolution:** event aliases must survive namespace/assembly refactors. Upcasters transform old CLR shapes or raw JSON into the current shape on read and should be pure, deterministic, fixture-tested functions ([Marten event versioning](https://martendb.io/events/versioning)). Never rewrite history as the routine schema-evolution mechanism.
- **Temporal hydration:** aggregate a stream to its latest state, a specified stream version, or a timestamp. Marten exposes all three and demonstrates the expected semantics ([Marten live aggregation](https://martendb.io/events/projections/)). Our aggregate repository should make `LoadLatest`, `LoadAtVersion`, and `LoadAsOf(instant)` explicit; the timestamp query must define database timestamp, inclusivity, and tie/order behavior.
- **Projection lifecycles:** inline projections execute in the event-append unit of work; live projections hydrate on demand and persist nothing; asynchronous projections checkpoint and eventually update durable read models. Use those three names and consistency contracts rather than one ambiguous “projection” mechanism ([Marten projections](https://martendb.io/events/projections/), [inline transaction behavior](https://martendb.io/events/projections/inline)).
- **Multi-stream projections:** Marten defaults them to asynchronous because concurrent writers can overwrite/stomp updates and appear to skip events under load ([multi-stream projections](https://martendb.io/events/projections/multi-stream-projections)). Treat cross-aggregate views as async unless a specific locking/concurrency design is proven.
- **Rebuild side effects:** replaying a new projection version can repeat messages or appended events for all history. Projection code should be pure database transformation by default; external effects must be separately gated, idempotent, or disabled during rebuild. Marten documents the exact blue/green replay hazard ([projection rebuilding](https://martendb.io/events/projections/rebuilding), [projection side effects](https://martendb.io/events/projections/side-effects)).

**Recommendation:** borrow the invariants and operational contracts, not Marten's public API or table names wholesale. The spike must prove deterministic decider hydration, historical reads, expected-version conflicts, atomic inline projection updates, versioned fixtures/upcasters, checkpointed async rebuild into shadow tables, verification/swap, cancellation/resume, and suppression of external side effects.

## 8. Repository tooling baseline

| Tool | Current baseline | License / compatibility | Recommendation |
| --- | ---: | --- | --- |
| pnpm | 12.5.1 | MIT for the TypeScript pnpm CLI; Node `>=18`. The repository's experimental `pnpr/` component has a separate PolyForm Shield license but is not the CLI proposed here. | Adopt and pin directly plus through the `packageManager` declaration; commit only `pnpm-lock.yaml`. Do not require Corepack because Node stops bundling it in v25. Aspire's C# host can invoke pnpm directly, so there is no npm exception. |
| Prettier | 3.9.8 | MIT; Node `>=14` | Adopt for JS/TS/JSON/YAML/Markdown where its parser is appropriate; run `--check` in CI. |
| commitlint CLI + conventional config | 21.2.3 | MIT; Node `>=22.12`, compatible with the VM's Node 24. Node 24 repositories should contain `package.json` or use an `.mjs` config. | Adopt for Conventional Commit syntax, but enforce commit ranges again in CI because local hooks are bypassable. |
| Lefthook | 2.1.14 | MIT; single Go binary distributed through several package managers, including npm | Adopt as the cross-language hook runner. Keep hooks fast and staged-file scoped; run authoritative full checks in CI. |
| CSharpier | 1.3.0 | MIT; .NET local tool | Defer initially. Use root `.editorconfig` plus the SDK formatter first. If the team wants a deliberately opinionated Prettier-like C# layout, pin CSharpier in the local tool manifest and make it the sole C# layout formatter. |
| `.editorconfig` + `dotnet format` | .NET 10 SDK | .NET SDK is MIT; EditorConfig/code-style support is built into the SDK/toolchain | Adopt. Root `.editorconfig` owns whitespace, naming, and analyzer severity; CI runs `dotnet format --verify-no-changes` plus build/analyzers. |
| `.slnx` | Native in .NET 10 CLI | No extra dependency; `dotnet new sln` creates `.slnx` in .NET 10 and `dotnet sln ... migrate` converts `.sln` | Adopt one root `.slnx`; verify every required IDE/CI integration before removing any temporary `.sln` compatibility file. |

Sources: official npm registry metadata queried on the research date; [pnpm release and license](https://github.com/pnpm/pnpm/releases), [pnpm security/support policy](https://github.com/pnpm/pnpm/security), [commitlint releases and support](https://github.com/conventional-changelog/commitlint/releases), [Lefthook releases/license](https://github.com/evilmartians/lefthook), [CSharpier releases](https://github.com/belav/csharpier/releases), [.NET analyzer configuration with EditorConfig](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files), [.NET code-style build enforcement](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview), and [`dotnet sln`/`.slnx`](https://learn.microsoft.com/dotnet/core/tools/dotnet-sln).

Do not make pre-commit hooks run the entire Testcontainers or Aspire suite. A practical split is formatting and cheap static checks pre-commit, commitlint at `commit-msg`, and build/unit/application tests plus all integration/topology checks in CI. Hooks improve feedback; branch protection and CI remain the policy boundary.

## 9. Microsoft Agent Framework

There is no current AI feature, so MAF should not enter the baseline. It is now mature enough to revisit around a real use case: stable `Microsoft.Agents.AI` 1.22.0 was published on 2026-09-18, targets .NET 8+, and is MIT licensed ([NuGet](https://www.nuget.org/packages/Microsoft.Agents.AI)). Microsoft positions Agent Framework as the forward foundation from the AutoGen and Semantic Kernel teams and documents agents, sessions, memory, workflows, hosting, and migration ([getting started](https://learn.microsoft.com/en-us/agent-framework/get-started/), [migration direction](https://learn.microsoft.com/en-us/agent-framework/migration-guide/from-autogen/)).

Several provider/A2A/MCP/hosting paths still use prerelease packages, and recent releases contain breaking changes ([self-hosting warning](https://learn.microsoft.com/en-us/agent-framework/hosting/self-hosting), [official releases](https://github.com/microsoft/agent-framework/releases)). When the product has an AI workflow, add one isolated executable sample, pin all packages, evaluate data leakage/prompt injection/cost/authorization, and avoid a speculative shared “AI framework” across modules.

## 10. License recommendation

Keep this repository under **Apache License 2.0**. It permits private/commercial use, modification, sublicensing, and source or binary redistribution; it is permissive rather than copyleft, so a derived real product may remain proprietary. Compared with MIT, Apache-2.0 includes an express contributor patent grant and a patent-litigation termination clause.

Redistribution still requires a license copy, preservation of applicable notices, prominent notices of modified files, and propagation of relevant `NOTICE` content. It grants no trademark rights ([Apache-2.0 text](https://www.apache.org/licenses/LICENSE-2.0.html), [ASF license FAQ](https://www.apache.org/foundation/license-faq)).

Add or keep a root `LICENSE`, add a minimal `NOTICE`, and document how copied/scaffolded products preserve attribution. Review every dependency's license in central package updates; permissive project licensing does not neutralize an incompatible dependency. This is an engineering recommendation, not legal advice.

## 11. Decisions and gates suggested by this research

| Topic | Suggested status | Decision/gate |
| --- | --- | --- |
| Azure | Direction accepted | Design for Azure-managed dependencies without leaking Azure SDK types into module contracts. |
| AKS | Proposed, not final | Compare AKS with Container Apps/App Service at the deployment ADR; select AKS only with explicit operational rationale and owner. |
| Gateway | Proposed | Front Door Premium + private origin + AKS managed Gateway API for production; validate the topology in real AKS. |
| Spend safety | Required | Threat model, WAF rules, app/business quotas, scale ceilings, log budget, Azure policies, and incident runbook must exist before public exposure. |
| Messaging | Proposed | RabbitMQ local failure suite; Service Bus emulator compatibility; real Service Bus pre-release suite; Rebus if one handler model spans both. |
| Outbox/inbox | Proposed | Small repository-owned EF/PostgreSQL building-block libraries are justified after the first two modules need them; keep event contracts separate from transport types. |
| Authentication | Direction accepted | Keycloak/Entra issue identity only; product owns authorization. |
| Entra tenant type | Open | B2B workforce collaboration versus External ID customer tenant depends on product onboarding/audience. |
| Redis | Conditionally justified | Use for distributed BFF tickets only after the BFF/multi-replica requirement; target Azure Managed Redis, not retiring Azure Cache for Redis. |
| Data Protection | Proposed | Blob key ring + versionless Key Vault key + stable app name; not Redis as the durable key store. |
| Local secrets | Proposed | Aspire/user-secrets and disposable credentials; no dependency on live Key Vault. |
| AppHost | Proposed | Conventional C# project to retain Aspire testability. |
| k3s/Flux | Proposed proof | Validate portable deployment/GitOps behavior locally, then repeat Azure-specific proof on AKS. |
| Repository tooling | Proposed | pnpm + Prettier + commitlint + Lefthook; root `.editorconfig`, native `dotnet format`, and root `.slnx`; defer CSharpier unless its opinionated formatting is deliberately selected. |
| MAF | Deferred | Revisit only for a named product AI workflow. |
| License | Proposed | Apache-2.0 plus `NOTICE`. |
