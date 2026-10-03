# Cheapest Feasible Azure Footprint

Status: Research input for architecture review; not an implementation decision record.

As of: 2026-09-23

## Purpose, method, and pricing assumptions

This note answers a narrower question than the broader Azure runtime research: what is the cheapest setup that still exercises the architecture honestly, and where does that setup stop being production-ready?

It uses primary sources only. Prices are public pay-as-you-go retail rates in USD for **East US**, sampled from Microsoft's Retail Prices API on the date above, with 730 hours/month, no tax, support plan, reservation, negotiated discount, egress, or unusual operation volume. Product pricing pages are estimates rather than quotes, and some meters are tiered. Recreate a signed-in Azure Pricing Calculator estimate immediately before deployment ([Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices), [Pricing Calculator guidance](https://learn.microsoft.com/en-us/azure/cost-management-billing/costs/pricing-calculator)).

The numbers below are deliberately rounded planning figures. A low bill is not evidence of availability, recoverability, security, or supportability.

## Executive recommendation

- **Development experiment:** run PostgreSQL, RabbitMQ, Redis, Keycloak, Mailpit and Azurite on the existing VM through Aspire. Use no Azure service by default. Incremental Azure cost can be **$0/month**; create short-lived Azure resources only for compatibility tests.
- **Files:** use an application-owned attachment/document capability backed by Blob Storage, not an OS file share. Run it against Azurite locally. Store blob identity and business metadata in the owning module's PostgreSQL schema; do not put attachment bytes in the database by default.
- **Identity:** use Keycloak locally, keeping the application dependent only on OIDC/JWT behavior. For an independent multi-tenant ERP SaaS, the cheapest sensible production default is a Microsoft Entra External ID **external tenant**: core identity is free for the first 50,000 monthly active users. Use workforce-tenant B2B collaboration only when the application is owned by one organization and its invited partners are guests of that organization's directory.
- **Messaging:** use RabbitMQ only during the local experiment. Rebus keeps the handler model portable. The cheapest Azure Service Bus tier that supports topics/subscriptions is **Standard**, with a $10/month base charge and the first 13 million operations included. Basic is disqualified because it is queues-only.
- **Cheapest Azure-hosted private pilot:** prefer a consumption-plan Azure Container App plus non-HA PostgreSQL `B1ms`, one-node Azure Managed Redis `B0`, Service Bus Standard, ACR Basic, Blob Storage, Key Vault and tightly capped Azure Monitor. A credible low-traffic planning band is **$45-$70/month**, before egress/DNS/support. It deliberately contains single points of failure and is not production-ready.
- **Production:** do not put AKS on the critical path. Container Apps remains the lower-cost default for one process unless Kubernetes is selected for operational reasons. A single-region, multi-replica baseline with PostgreSQL HA, Redis HA, a protected edge and controlled telemetry is roughly **$650-$850/month**. Requiring Service Bus Premium for Private Link/predictable capacity adds about **$667/month**. These are architecture bands, not quotes.

## 1. Application files and Azurite

### What Azurite is and is not

Azurite is Microsoft's open-source, MIT-licensed Azure Storage emulator. It supports **Blob, Queue and Table** services; Table support remains preview. It explicitly does **not** support Azure Files or Data Lake Storage Gen2, and it differs from the cloud service in endpoints, scale and some protocol behavior ([Azurite overview and differences](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite), [Azurite repository and MIT license](https://github.com/Azure/Azurite)).

Aspire's Azure Storage hosting integration can call `RunAsEmulator()`. Aspire then starts Azurite, injects local connection information, and can attach a persistent container volume. Removing `RunAsEmulator()` changes the resource to real Azure Storage for deployment ([Aspire Azure Storage emulator integration](https://aspire.dev/integrations/cloud/azure/azure-storage-tables/azure-storage-tables-host/)).

### Recommendation for ERP documents

“Files” should mean ERP attachments such as invoices, purchase documents and product images, not an Azure Files share. Blob Storage is designed for unstructured text/binary data and serving documents; Azure Files is a managed SMB/NFS share primarily useful for lift-and-shift applications or consumers that require filesystem semantics ([Blob Storage overview](https://learn.microsoft.com/en-us/azure/storage/blobs/storage-blobs-overview), [Azure Storage service comparison](https://learn.microsoft.com/en-us/azure/storage/common/storage-introduction)).

Use Blob Storage in Azure and Azurite Blob locally. Define an application-facing attachment capability around opaque object identifiers, content type, length/checksum and streaming upload/download. Persist business ownership, authorization, original filename, retention state and audit facts in the owning module. Treat object storage as persistence, not as the authorization model: downloads pass through an authorized application endpoint or receive a narrowly scoped, short-lived URL.

Do not introduce MinIO or a local filesystem backend merely to avoid Azurite. Either would create another semantic variant to maintain. Reconsider only if cloud-neutral S3 compatibility or true mounted-file behavior becomes a product requirement. Azurite is for development/test, not durable production storage; tests that depend on Azure-specific authorization, lifecycle, immutability, malware scanning, networking or scale still need a short-lived real storage account.

At the sampled East US retail rates, Standard general-purpose v2 Hot LRS block blobs start around **$0.0208/GB-month**, with writes around **$0.05/10,000** and reads around **$0.004/10,000**. Ten GiB of pilot documents is therefore well below $1/month before transfer and ancillary operations ([Blob Storage pricing](https://azure.microsoft.com/en-us/pricing/details/storage/blobs/), [Retail Prices API](https://learn.microsoft.com/en-us/rest/api/cost-management/retail-prices/azure-retail-prices)).

## 2. Cheapest identity that fits the product

The application owns tenants, memberships, roles, permissions, approval limits and other authorization. The identity provider supplies authentication and stable identity claims only. Use immutable `(issuer, subject)` as the external identity key; do not make email the durable user identifier.

| Choice | Direct identity charge at small scale | Fits | Does not fit / hidden cost |
| --- | ---: | --- | --- |
| Keycloak on the existing VM | $0 incremental Azure; Apache-2.0 software | Local development, offline testing and full control | In Azure it consumes compute, database, backup, upgrades and on-call labor; “free software” is not a free production identity service. |
| Entra External ID, workforce tenant with B2B collaboration | Core features free for first 50,000 aggregate external MAU | One enterprise exposes its own apps/data to invited partner users who retain their home identities | Customer users become guests in that enterprise's workforce directory; this is not automatically the right SaaS customer-directory model. |
| Entra External ID, external tenant | Core features free for first 50,000 aggregate external MAU | An independent SaaS publishes an app to business customers/consumers with a separate customer directory and branded sign-up | Premium add-ons, SMS and machine-to-machine token transactions are separate meters. |

Microsoft describes workforce tenants as employee/internal-resource directories using B2B collaboration, while external tenants are separate CIAM directories for apps published to consumers or business customers ([External ID tenant models](https://learn.microsoft.com/en-us/entra/external-id/external-identities-overview), [B2B collaboration](https://learn.microsoft.com/en-us/entra/external-id/what-is-b2b)). External ID combines linked workforce and external-tenant external users for MAU billing and requires a linked Azure subscription. Core/basic interactive identity has a free tier; add-ons are billed separately ([External ID billing model](https://learn.microsoft.com/en-us/entra/external-id/external-identities-pricing)). Microsoft states that core External ID features are free for the first **50,000 MAU** ([Entra licensing](https://learn.microsoft.com/en-us/entra/fundamentals/licensing)).

The public Retail Prices API currently exposes both a newer `Basic` meter at **$0.03/MAU above the 50,000 tier** and an older `Core` meter at **$0.01625/MAU above the tier**. Because Microsoft is transitioning offer names and the public pricing page can be account-sensitive, do not use the lower legacy-looking meter in a budget; confirm which offer the new tenant actually receives. Both choices are $0 for the intended experiment/private-pilot scale. The first 50,000 allowance is shared across linked tenants rather than repeated per tenant.

**Proposal:** choose an External ID external tenant for the eventual SaaS unless the product becomes a single enterprise's partner portal. Keep Keycloak locally and run a small issuer-conformance suite against both. Do not encode Keycloak- or Entra-specific groups as product permissions.

Do not select legacy Azure AD B2C for a new product: it stopped being available to new customers on 2025-05-01; Microsoft directs new CIAM work to Entra External ID ([Azure AD B2C FAQ](https://learn.microsoft.com/en-us/azure/active-directory-b2c/faq)).

## 3. Broker floor: RabbitMQ locally, Service Bus Standard in Azure

Azure Service Bus tiers are not interchangeable for this design:

| Tier | Topics/subscriptions | Current public East US floor | Decision |
| --- | --- | ---: | --- |
| Basic | No; queues only | Operations meter, no Standard base | Reject for integration events because new consumers require fan-out. |
| Standard | Yes; also sessions, transactions and duplicate detection | **$10/month base**, first 13M operations included | Cheapest feasible Azure tier for Rebus publish/subscribe and low-throughput pilot/production. |
| Premium | Yes, dedicated messaging units, Private Link/VNet and predictable performance | **$0.9275/MU-hour**, about **$677/month** for one MU | Add only when isolation, private networking or measured throughput/latency requires it. |

Microsoft's tier comparison explicitly says Basic supports queues only and Standard adds topics/subscriptions, sessions, transactions and duplicate detection ([Service Bus tier comparison](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-premium-messaging)). A topic gives every subscription its own copy, which is the required “add a consumer later” fan-out primitive ([queues, topics and subscriptions](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-queues-topics-subscriptions)). The Standard service is shared capacity and Microsoft recommends it for development, QA and low-throughput production where variable latency/throttling is acceptable ([Service Bus throttling](https://learn.microsoft.com/en-us/azure/service-bus-messaging/service-bus-throttling)). Current operation allowances and billing rules are on the [Service Bus pricing page](https://azure.microsoft.com/en-us/pricing/details/service-bus/).

Use RabbitMQ alone for the main local failure suite: it is a real durable broker and exposes the acknowledgement, consumer-crash, redelivery, poison-message and dead-letter behavior that must be proved. Rebus makes switching transports comparatively small at the handler/composition layer, but it does **not** make broker topology, limits, sessions, dead-letter operations, authentication or production failure behavior identical. Run a small pre-release compatibility suite against a short-lived real Service Bus Standard namespace. The emulator is optional rather than part of the default topology.

## 4. PostgreSQL Flexible Server floor

### Cheapest workable pilot database

The East US Retail Prices API currently lists PostgreSQL Flexible Server Burstable `B1ms` compute at **$0.017/hour**, about **$12.41/month** continuously. The smallest useful storage allocation is 32 GiB; current storage pricing is **$0.115/GiB-month**, about **$3.68/month**. The resulting always-on floor is about **$16.09/month**, before excess backup or network charges. A `B2s` is **$0.068/hour** (about $49.64/month compute) if load tests show the `B1ms` is inadequate.

Flexible Server includes automated backup storage up to 100% of provisioned primary storage; excess LRS backup is currently **$0.095/GiB-month**. Retention can be 7-35 days. The pricing page also confirms that stopped servers incur storage/backup but no compute charges ([PostgreSQL Flexible Server pricing](https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/), [backup/business-continuity behavior](https://learn.microsoft.com/en-us/azure/postgresql/backup-restore/concepts-business-continuity)). A stopped server automatically starts after seven days, so stop/start is useful for intermittent test environments but is not indefinite scale-to-zero ([Flexible Server overview](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/service-overview)).

### Reliability boundary

Burstable compute is intended for low-cost development and low-concurrency workloads, has a 99.9% single-server SLA, and **does not support HA**. It also lacks read replicas and the built-in PgBouncer capability. This is acceptable for an explicitly non-HA private pilot with rehearsed point-in-time restore, but not the production baseline ([HA limitations](https://learn.microsoft.com/en-us/azure/postgresql/flexible-server/concepts-high-availability), [service tiers](https://learn.microsoft.com/en-us/azure/postgresql/configure-maintain/quickstart-create-server)).

The lowest honest production starting point is General Purpose with HA. Using the presently sampled two-vCore General Purpose compute meter at **$0.178/hour**, primary plus standby compute is about **$260/month** before storage. Microsoft bills both replicas' compute and storage, and zone-redundant HA targets 99.99% versus 99.95% for same-zone HA. Actual SKU availability and minimum supported storage must be checked at deployment time.

Do not use customer-managed database encryption keys merely to justify Key Vault. Flexible Server always encrypts data at rest with service-managed keys; customer-managed keys add Key Vault and identity dependencies and should follow a named compliance requirement ([PostgreSQL encryption](https://learn.microsoft.com/en-us/azure/postgresql/security/security-data-encryption)).

## 5. Small necessary Azure services

| Service | Cheapest useful choice | Low-volume monthly anchor | Boundary |
| --- | --- | ---: | --- |
| Key Vault | Standard | Usually cents: **$0.03/10,000 operations**, no ordinary vault base meter | Use for Azure-deployed secrets/keys through workload/managed identity. Not needed locally. HSM keys, rotation and certificate renewal have separate charges. |
| Blob Storage | Standard GPv2, Hot LRS initially | 10 GiB plus light operations: **< $1** | LRS is not zone/region disaster protection. Add lifecycle rules and stronger redundancy when recovery requirements demand it. |
| Azure Container Registry | Basic | **$0.1666/day**, about **$5/month**, 10 GB included | Adequate for one image and a pilot; no Private Link or geo-replication. An external registry could remove this cost, but ACR keeps image pull identity and region locality simple. |
| Azure Monitor / Application Insights | PAYG Analytics Logs with sampling/caps | **$0** while the billing account stays within the first 5 GB/month free allowance | Telemetry has no automatic hard spending cap. Sample traces, filter health/noise, set daily caps/alerts and keep audit records out of diagnostic logs. Current East US Analytics ingestion beyond the allowance is roughly $2.30/GB. |
| Azure Managed Redis | Balanced `B0`, one node for pilot | **$0.016/node-hour**, about **$11.68/month** | Non-HA mode has no availability SLA and Microsoft says it is dev/test only. Two-node HA is about $23.36/month before data/traffic and is the production floor. |

Sources: [Key Vault pricing](https://azure.microsoft.com/en-us/pricing/details/key-vault/), [ACR pricing and included storage](https://azure.microsoft.com/en-us/pricing/details/container-registry/), [ACR tier capabilities](https://learn.microsoft.com/en-us/azure/container-registry/container-registry-skus), [Azure Monitor pricing/free allowance](https://azure.microsoft.com/en-us/pricing/details/monitor/), [Application Insights billing](https://learn.microsoft.com/en-us/azure/azure-monitor/app/application-insights-faq), and [Azure Managed Redis non-HA guidance](https://learn.microsoft.com/en-us/azure/redis/architecture).

Use Azure Managed Redis for a new Azure design, not the older Azure Cache for Redis tiers. Microsoft blocked creation of those tiers for new customers in April 2026 and retires Basic, Standard and Premium on 2028-09-30 ([retirement timeline](https://learn.microsoft.com/en-us/azure/azure-cache-for-redis/retirement-faq)).

The application process on Azure Container Apps Consumption can scale to zero and incurs no resource-consumption charge while at zero. The monthly subscription grant is 180,000 vCPU-seconds, 360,000 GiB-seconds and two million requests. An always-ready `0.25 vCPU/0.5 GiB` replica costs roughly **$0-$20/month** at current rates depending on whether it is idle/active and how much of the shared grant other apps consume ([Container Apps scaling](https://learn.microsoft.com/en-us/azure/container-apps/scale-app), [Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)). Cap maximum replicas to bound bot-driven compute spend.

## 6. Three deliberately different environments

### A. Development experiment: existing VM, approximately $0 incremental Azure

Run through Aspire:

- the application and minimal frontend;
- PostgreSQL;
- RabbitMQ;
- Redis;
- Keycloak;
- Mailpit;
- Azurite Blob.

Use Aspire secret parameters/.NET user secrets, not Azure Key Vault. Use the Aspire dashboard/local OpenTelemetry pipeline, not Azure Monitor. Persist development volumes only when a workflow needs restart continuity; integration tests should create disposable infrastructure.

Use Azure only for narrow compatibility checks:

- a short-lived Service Bus Standard namespace before releases;
- a short-lived storage account for Azure-only Blob behavior;
- an Entra External ID test tenant when identity integration starts.

This maximizes learning per dollar and avoids testing Azure billing before the architecture exists. It does not validate managed identity, Private Link, Azure outages, cloud quotas or production operations.

### B. Cheapest Azure-hosted private pilot: approximately $45-$70/month

| Component | Pilot assumption | Approximate monthly cost |
| --- | --- | ---: |
| Application | Container Apps Consumption, max replicas tightly capped, scale-to-zero or one small idle replica | $0-$20 |
| PostgreSQL | Flexible Server `B1ms`, 32 GiB, non-HA | $16 |
| Broker | Service Bus Standard | $10 |
| Tickets/cache | Azure Managed Redis `B0`, one node, non-HA | $12 |
| Image registry | ACR Basic | $5 |
| Identity | Entra External ID external tenant, under 50,000 MAU | $0 |
| Secrets/files/telemetry | Key Vault operations, 10 GiB Hot LRS blobs, <=5 GB Analytics Logs | $0-$2 |
| **Planning total** | Before egress, DNS, support and taxes | **$45-$70** |

This is a private-pilot topology, not a public production promise. The database and Redis can become unavailable; Container Apps may cold-start; Service Bus Standard can throttle; ACR Basic and public service endpoints limit private-networking choices; backups still need restore drills. Prefer allow-listed/private pilot access and hard Container Apps scale ceilings. If the pilot must be always available or carry contractual data, move to the next class.

### C. Single-region production-ready baseline: approximately $650-$850/month

Assume:

- two or more application replicas on Container Apps Consumption with bounded maximum scale;
- PostgreSQL two-vCore General Purpose with zone-redundant HA, automated backups and restore drills (~$270 before workload-driven storage growth);
- Azure Managed Redis `B0` with two-node HA (~$23);
- Service Bus Standard ($10) while shared capacity and a public endpoint with Entra authentication are acceptable;
- ACR Basic, Blob Storage and Key Vault;
- Azure Front Door Premium/WAF/private origin at roughly $330 base, pending the gateway ADR;
- carefully sampled Azure Monitor ingestion.

That yields a rough **$650-$850/month** floor before substantial traffic, egress, support, email, long retention or cross-region DR. If security requires Service Bus Private Link or the workload requires dedicated predictable broker capacity, replace Standard with one Premium messaging unit: current public pricing is about **$677/month**, adding roughly $667/month over Standard. Likewise, stronger blob redundancy, larger database/Redis sizes, DDoS products and multi-region recovery increase the range.

The application/data reliability floor **without a public edge/gateway** is closer to **$325-$450/month**: roughly $270 PostgreSQL HA, $23 Redis HA, $10 Service Bus Standard, $5 ACR, $25-$60 application compute and a modest allowance for storage/telemetry. That is useful for a privately reachable production workload or cost comparison, but it is not the public-internet baseline because bot/WAF/private-origin protection remains unresolved.

“Production-ready” must be tied to explicit SLO/RPO/RTO and threat-model decisions. Two replicas and HA data services are not automatically sufficient for every ERP.

## 7. Cost-control rules to carry into the plan

1. Keep AKS, Flux and k3s after the application architecture and first deployable slice. k3s is useful for packaging/GitOps learning but does not prove AKS operations.
2. Give every elastic resource a maximum: Container Apps replicas, request concurrency, body sizes, database pool size/timeouts, broker consumer concurrency and retries, telemetry volume and export/job limits.
3. Put anonymous/IP limits before authentication and subject/tenant/business-operation quotas after authentication. A budget alert does not stop consumption.
4. Default integration events to compact payloads; store large documents in Blob and send identifiers. This avoids message-size and operation multiplication.
5. Treat a production broker endpoint as a security decision. Standard is the price floor; Premium is not justified merely because it exists, but private networking may justify it.
6. Set cost and anomaly alerts from day one, but pair them with a rehearsed kill switch: reduce max replicas, block abusive routes/identities, pause expensive consumers and disable nonessential exports.
7. Reprice before each environment promotion. Pinning code dependencies does not pin cloud prices or regional SKU availability.

## 8. Proposed decisions and open gates

### Proposed now

- Adopt Azurite Blob for local ERP attachment storage; Azure Blob Storage is the production target.
- Use Keycloak only for local identity and Entra External ID external tenant as the current production proposal.
- Use RabbitMQ as the default local broker and Service Bus Standard as the cheapest Azure broker capable of integration-event fan-out.
- Run the experiment entirely on the existing VM and delay Kubernetes work.
- Use Container Apps, not AKS, for the cheapest Azure pilot comparison.

### Gates before implementation/deployment

- Define attachment ownership, retention, virus-scanning and download authorization requirements.
- Confirm whether the product is independent SaaS CIAM (external tenant) or one enterprise's partner portal (workforce B2B).
- Verify Rebus topology and error/dead-letter behavior against RabbitMQ and real Service Bus Standard.
- Load-test `B1ms`; promote to a larger burstable or General Purpose SKU before CPU-credit/connection constraints become an incident.
- Decide whether production requires private endpoints for Service Bus, PostgreSQL, Redis, Blob and Key Vault. That choice can dominate the cost more than application compute.
- Set SLO, RPO/RTO, telemetry budget and gateway/bot-protection requirements before calling any topology production-ready.
