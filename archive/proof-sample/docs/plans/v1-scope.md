# V1 Scope and Deferred Register

Status: Accepted architecture baseline; implementation follows the separately approved v1 delivery plan.

Last reviewed: 2026-09-23

## Rule

Record future concerns so they are not forgotten, but create no v1 code, interface, table, package, container, deployment manifest, or test harness for a deferred concern. Promote an item only when its stated trigger is present in an accepted product scenario.

This scope is a current implementation baseline, not a preservation mandate. Before the first real delivery, replace or rebuild accepted work when implementation evidence exposes a cleaner design; update the plan, tests, and ADR status at the same time. Compatibility work becomes mandatory only once real durable data or external consumers depend on the previous shape.

## Required for v1

| Capability | Smallest justified implementation |
| --- | --- |
| Repository structure | One `.slnx`, central build/package policy, root `apps`/`modules`/`shared` areas, four module pairs, API, conventional C# Aspire AppHost project, finite Migrator project, focused shared infrastructure, tests, and durable documentation/agent guidance. |
| Module boundaries | Access, Sales, Inventory, and Purchasing schemas/migrations/contracts with compiler and architecture-test enforcement. |
| Organization management | Create an organization with a customer-proposed immutable slug; create the first administrator membership; list memberships; invite, accept, suspend/remove, and assign system roles; prevent removal of the last administrator. Support the same application flow with open-registration and directory-gated OIDC providers. No billing, custom domains, or organization deletion workflow. |
| Authentication | Keycloak locally; BFF secure cookie with Redis-backed server-side ticket; immutable issuer/subject identity link. Entra External ID compatibility belongs to the Azure deployment phase. |
| Authorization | Membership-scoped code-defined system roles, persisted assignments, module-defined stable permission tokens, handler enforcement, Sales-owned approval policy, and only HumanActor plus the actual Fulfilment WorkflowActor. Organization Administrator manages access only and is not a business superuser. |
| Tenant isolation | URL organization slug, verified request tenant context, required discriminator, scoped indexes, query filters, write validation, and cross-tenant tests. |
| Sales workflow | A minimal Sales Order path sufficient to submit/confirm, start fulfilment, observe reservation/shortage state, cancel, and audit the outcome. |
| Inventory proof | Inventory-owned minimal Stock Items and Stocking Locations plus one event-sourced Stock Position per organization/location/item. It uses constrained decimal quantities in one immutable base unit, child Reservations, non-negative stock invariants, optimistic append, deterministic hydration through the same evolution entry point, an inline current-state projection, recorded-time state/history queries, immutable correction events, and only the event evolution mechanisms exercised by its persisted fixtures. |
| Late event-sourcing validation | Increment 8.1 adds a second concrete event-sourced aggregate, preferably in another module, to test distinct decision-state and multiple-inline-view needs before extracting libraries. Its owning charter and domain are chosen then; it does not convert the first workflow's other aggregates implicitly. |
| Durable fulfilment | RabbitMQ plus Rebus transport adapters, one isolated input/error endpoint per asynchronous consumer module, broker fan-out for integration events, direct routing for integration commands, module-owned EF inbox/outbox/process/deadline records, line-level idempotency, bounded technical retries, and idempotent Reservation Release compensation. |
| Purchasing participation | Create and observe a Replenishment Requirement from a real shortage. Purchase-order lifecycle depth is limited to what the accepted first workflow exercises. |
| Audit and activity | Per-module audit for accepted changes and security-significant denials; one curated Sales Order activity timeline. Raw event JSON is not a product timeline. |
| Files | Architecture decision and adapter seam are recorded. Add Azurite/Blob runtime wiring only if the accepted v1 workflow includes a real attachment operation. |
| Email | Mailpit and a minimal sender only if organization invitation is email-based; no general notification framework. |
| Observability | Aspire dashboard/OTel defaults plus traces and metrics for HTTP, PostgreSQL, outbox/inbox, message handling, process age, and projection failure. |
| Tests | Domain/application tests, architecture tests, PostgreSQL/Testcontainers integration tests, RabbitMQ failure tests, HTTP API tests, and only the browser smoke needed to prove BFF login and the critical workflow. |
| Deployment | One application image, migration job, cheapest feasible Azure private-pilot path, secrets/identity/storage appropriate to that environment, cost ceilings, smoke test, backup/restore, and rollback evidence. |
| Extraction support | Consumer-owned ports, versioned integration contracts, snapshot-plus-tail guidance, compatibility fixtures, and a documented migration playbook. No service extraction is performed in v1. |
| Product scaffolding | After second-aggregate proof and focused library/sample separation, rehearse bounded configuration-driven creation of a real product repository and deploy it. Naming is the initial configuration; untested provider/persistence options and a broad extensible generator remain excluded. |

## Reference scenario capability coverage

The wholesale ERP is a coherent reference domain used to exercise architecture capabilities, not an attempt at feature-complete ERP software. A domain behavior belongs in v1 only when it is necessary for a credible invariant or proves a named capability below.

| Reference behavior | Capability it must prove |
| --- | --- |
| Organization creation, invitation, and system roles | Multi-tenancy, JIT external identity linkage, product authorization, BFF navigation, email outbox, and audit. |
| Sales Order submission and approval | DDD aggregate behavior, vertical slice, handler authorization, separation of duties, approval policy, module-local transaction, audit, and activity timeline. |
| One Stock Position per organization/location/SKU | Self-built event stream, decider/evolution, optimistic concurrency, inline projection, recorded-time history, curated business timeline, correction events, and schema evolution fixtures. |
| Independent line reservation results | Versioned integration contracts, process correlation, idempotent consumers, partial process state, and avoidance of an unjustified multi-stream transaction abstraction. |
| Inventory shortage to Replenishment Requirement | Durable cross-module orchestration, third business-module participation, human wait, retry/reconciliation, and explicit ownership of the outcome. |
| Sales Order cancellation after reservation | Idempotent compensation through a workflow-only capability that is not an HTTP endpoint. |
| Consumer bootstrap proof | Versioned snapshot plus high-watermark and tail consumption without replaying another module's private domain-event stream. |
| One attachment, only if retained in the accepted workflow | Azurite/Blob adapter, streaming authorization, metadata ownership, and audit; otherwise the runtime resource remains deferred. |
| Private Azure pilot | One deployable image, migrations, managed dependencies, secret handling, spend ceilings, telemetry, backup/restore, and rollback. |

Domain completeness that proves no additional capability is deferred. Conversely, a fake domain transition is not acceptable merely because it touches infrastructure: failure, authorization, replay, and compensation tests require coherent business states.

Detailed ERP policy is not an architecture-review gate. The reference implementation may choose simple coherent defaults—such as explicit zero-quantity Stock Position creation and all-or-nothing reservation per order line—without promoting them into reusable foundry abstractions.

## Proofs required before broad adoption

| Item | V1 proof boundary |
| --- | --- |
| Self-built event sourcing | Start inside Inventory with one aggregate family and require concurrency, replay, inline-projection rollback, schema evolution fixture, and as-of reconstruction tests. A second concrete aggregate is explicitly scheduled in Increment 8.1 before library extraction. |
| Inbox/outbox/process infrastructure | Extract shared EF mechanics only after at least two real module consumers expose identical needs. Business process state and transitions remain concrete. |
| Azure Service Bus | Run a focused real-Standard-namespace compatibility suite before Azure release; do not add the emulator to the default local topology. |
| Entra External ID | Run issuer/audience/login/logout conformance against the Azure environment; product authorization remains unchanged. |
| Shared abstractions | Require two concrete consumers or two real adapters. Until then keep implementation local and easy to replace. |

## Deferred register

| Deferred item | Adoption trigger |
| --- | --- |
| Cross-module shared PostgreSQL transaction spike | A named short workflow has a demonstrated all-or-nothing invariant that cannot tolerate the already selected durable workflow. The first fulfilment flow does not qualify. |
| Rebus `Saga<TData>` / `IdempotentSaga<TData>` spike | The concrete EF process manager reveals material persistence/correlation complexity that Rebus can remove without weakening the single module transaction. |
| Distributed global technical-retry counter | Multi-replica failure tests show Rebus/broker safeguards can create unacceptable poison-message cost or load. |
| PostgreSQL row-level security | An adopting product threat model requires database-enforced tenant isolation and can operate the connection/transaction/migration role model. |
| OpenFGA | A real relationship graph appears: per-object sharing, nested groups, delegated partner access, deep inheritance, or reverse reachability queries. |
| Custom roles UI, direct grants, denies, inheritance, field permissions | A tenant requirement cannot be expressed with system roles plus module business policies. |
| SupportActor, impersonation, privileged access management | A real support workflow has an owner, approval/audit requirements, and a deployment environment that needs it. |
| Machine-to-machine identity | A real external client or extracted service must authenticate independently. Internal module calls and workflows do not qualify. |
| Product data cache in Redis | Measurement identifies a read path whose source, staleness, invalidation, tenant keying, and size policy are known. Redis remains justified for BFF tickets. |
| Separate hydration-checkpoint snapshots | Measured stream length/hydration cost justifies periodic checkpoints. Aggregate-shaped inline write models are recommended now; new-consumer snapshot-plus-tail remains a separate integration concern. |
| Resumable/online projection reconstruction | Measured stream length or recovery time justifies more than full reconstruction with atomic replacement. Durable progress requires reducer/projection revision compatibility and separation from serving state; reconsider at the 8.1b correctness gate. |
| Async projections | Explicitly wanted later with a concrete eventually consistent view. Start with one background worker, no leader election, and consumer-owned deployment scale. Competing workers require durable progress/claiming, per-view-key ordering, atomic effects/checkpoints, and idempotency proofs; do not implement a generic daemon now. |
| Multi-stream event-store transaction abstraction | A named invariant genuinely requires atomic appends across multiple Stock Position streams. Line-level reservation is chosen to avoid assuming this. |
| Generic event upcaster framework | A persisted event schema actually changes and a second version must be read. V1 keeps explicit stable names/versions and compatibility fixtures. |
| State-stored → event-sourced aggregate migration exercise | A later concrete requirement justifies changing persistence, or a separately approved learning exercise rehearses baseline import, historical gaps, cutover and verification. Sales remains state-stored for the first workflow; this observation does not add a required v1 migration or silently expand the second-aggregate proof. |
| Centralized audit search/export, tamper chains, redaction workflow | Compliance or operator discovery defines retention, evidence, search, immutability, and subject-data requirements. |
| General notification framework | A second real notification channel/use case repeats organization-invitation email mechanics. |
| Always-on Azurite/Blob attachment resource | A v1 use case uploads or downloads an attachment. Until then retain the decision and no runtime resource. |
| Custom domains/subdomain tenant routing | A paying tenant requires branding or its own domain and the DNS/certificate/OIDC lifecycle has an owner. |
| Restricted organization creation, Trial lifecycle, subscription entitlements, and billing | A product-commercial model defines who may create an organization, trial duration/limits, conversion, expiry, support behavior, and entitlement enforcement. V1 organization creation exists only to exercise the architecture and does not establish a permanent unrestricted-signup contract. |
| Service Bus emulator | A local-only Service Bus-specific behavior cannot be covered by RabbitMQ plus the focused real Azure compatibility suite. |
| RabbitMQ delayed-message plugin or Rebus timeout store | A non-business transport delay is needed. Business deadlines remain module-owned PostgreSQL rows. |
| Kubernetes, AKS, k3s, Helm/Kustomize, and Flux | The application is complete and deployed through the cheaper baseline, then a named operational reason justifies Kubernetes. |
| Public Azure edge/WAF/private endpoint topology | The deployment becomes public or its threat model/SLO requires the protected edge; the private pilot uses bounded application scaling and restricted access. |
| Actual module-to-service extraction | A module has an independent scaling, release, security, or ownership driver. V1 provides guidance and seams only. |
| Broad wizard / extensible generator / provider matrix | The final configured-scaffolding rehearsal demonstrates a real consumer need and exercised alternative implementations. A bounded naming/configuration script is already planned; generic generation infrastructure is not. |
| Separate Catalog module | Product behavior beyond a stockable item reference appears: non-stocked products, variants, merchandising, hierarchy, independently owned rich product content, or another capability with a distinct lifecycle. |
| AI/MAF integration | A product feature has a defined user outcome, data boundary, evaluation method, and cost/safety budget. |
| Full browser suite | The real product frontend has behavior that HTTP/application tests cannot cover. V1 keeps one critical BFF journey smoke test. |

## Explicitly removed from the v1 critical path

The following may not block the first workflow or first deployment: shared cross-module transactions, RLS, OpenFGA, Rebus sagas, distributed authorization caching, custom-role editing, support impersonation, general notifications, separate hydration-checkpoint snapshots, async projections, centralized audit, attachment runtime resources without an attachment use case, Kubernetes/Flux, actual service extraction, a broad generator, and AI features. The late second-aggregate/library/configured-scaffolding increments remain scheduled before the final product handoff.
