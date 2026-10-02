# V1 delivery slices

Status: Accepted delivery baseline.

Last reviewed: 2026-09-23

This plan turns the architecture roadmap into review-sized vertical increments. It is subordinate to the [architecture plan](architecture-and-delivery.md), [v1 scope](v1-scope.md), [module charters](../modules/README.md), and [repository workflow](../conventions/repository.md).

## Delivery rule

A **slice** is a capability and acceptance unit. An **increment** is the normal pull-request unit. A slice may contain several increments; combining increments into an oversized pull request is not the default. Each increment leaves the repository buildable, tested, and internally consistent, and every commit still requires explicit approval of its exact change set.

If an increment becomes difficult to review in one sitting, split it at a vertical, independently verifiable outcome. Do not split by creating speculative layers, empty abstractions, temporary architecture, or knowingly broken intermediate states.

### Execution order

Keep increment identifiers stable for historical references. The accepted order is to finish Slice 5, implement 8.1 and 8.1b, then complete Slice 6 before packaging/deployment. The remaining 5.6b diagnostics/recovery and 5.6c failure proofs are one workstream, delivered through self-contained review-sized checkpoints where needed. Increments 7.1–7.3 and 8.2–8.3 are temporarily deferred; resume them after those local proofs and explicit owner direction. This reordering does not pull library extraction forward or remove the eventual deployed-product goal.

### Formatter baseline

Use [CSharpier](https://csharpier.com/docs/Installation) as the pinned repository-local C#/XML layout formatter. Editor, Lefthook and CI share `.editorconfig` settings. Retain compiler/analyzer policy and limit `dotnet format` to semantic style and analyzer checks. Formatter adoption or upgrades and their mechanical baseline diffs are separate reviewable increments, not mixed with business slices.

## Reference outcome and invariants

The primary journey starts with a Sales Clerk and requires a distinct Sales Approver, with Organization Administrator, Sales Manager, Inventory Manager, and Purchasing Agent setup roles around it. The first valuable workflow is:

> An authenticated member creates or enters an Organization, prepares stocked goods, submits a Sales Order, has a different authorized member approve it, and can observe each line become reserved or create a Replenishment Requirement. Cancelling the order durably releases successful reservations. Every decision is tenant-isolated, authorized, idempotent where delivery can repeat, and auditable.

The workflow deliberately proves partial success across lines, a long-running process, compensation, a third business module, event-sourced Inventory state, and a late consumer. It does not attempt complete ERP behavior.

Cross-slice invariants:

1. The URL Organization slug selects context but never grants access; current membership is checked on every request or message-created actor context.
2. No module reads or writes another module's schema.
3. Immediate collaboration uses Contracts interfaces; durable commands use receiver-owned contracts; integration events use producer-owned contracts.
4. One order line is either fully reserved or short. Different lines may have different outcomes.
5. The submitter cannot approve the order. Approval permission and Sales-owned authority are rechecked at commit time.
6. Every broker handler is at-least-once safe through delivery inbox, stable business-operation identity, and domain invariants.
7. Stock Position events and its inline current projection commit atomically; replay produces no external effect.
8. Raw domain/event-store data is not a product audit or activity timeline.
9. Infrastructure appears only in the first increment that exercises it.

## CI lanes established by the increments

| Lane | Contents | Default frequency |
| --- | --- | --- |
| Fast | Restore, format verification, analyzers, build, domain/application tests, architecture tests | Every pull request |
| PostgreSQL | Migration, module contract, persistence, concurrency, transaction, and Testcontainers tests | Every pull request once introduced |
| Topology | `DistributedApplicationTestingBuilder`, Keycloak/Redis/Mailpit/RabbitMQ resource graph, critical HTTP/message path | Pull request when affected; required before merge to protected branch |
| Broker failure | Process termination, redelivery, duplicate, poison/error queue, relay lease recovery | Required for messaging changes; full matrix scheduled and before release |
| Browser | Critical BFF login and reference workflow | Required for frontend/auth changes; before release |
| Azure compatibility | Entra, Service Bus Standard, managed identity/secrets, deployed smoke/restore/rollback | Protected environment before Azure promotion |

The first increment creates the canonical repository commands for these lanes. A test is not complete until a CI lane runs it.

## Slice 1 — Repository and executable skeleton

### Increment 1.1 — Build policy and module boundaries

**Outcome:** a clean checkout restores and builds the intended project graph, and executable architecture tests reject forbidden references.

**Work:**

- Pin the selected .NET SDK and centrally manage build/package policy with `global.json`, `Directory.Build.props`, and `Directory.Packages.props`.
- Create the root `.slnx`, root `apps`/`modules`/`shared` areas, API, conventional C# AppHost, ServiceDefaults, Migrator, four `{Module}.Contracts`/`{Module}` pairs, and focused test projects.
- Add root `.editorconfig`, nullable/analyzer/warnings-as-errors policy, deterministic formatting, CI, Conventional Commit checking, and fast local Lefthook commands that do not run container suites.
- Add ArchUnitNET tests for the exact rules in the architecture plan, including Contracts package restrictions and API composition rules.
- Add scoped `AGENTS.md`/human README files only where a new significant source/test subtree now needs durable instructions.

**Excludes:** business entities, generic repositories, mediator, result framework, shared domain base library, event bus, database schema, and frontend tooling.

**Acceptance:** Fast lane passes on a clean checkout; deliberately introduced forbidden project references make architecture tests fail; all projects are reachable from the solution without circular references.

### Increment 1.2 — Local runtime, PostgreSQL, and finite migrations

**Outcome:** Aspire starts the API and PostgreSQL, runs the finite Migrator to completion, and exposes health and OpenTelemetry without business behavior in the API.

**Work:**

- Compose PostgreSQL, Migrator, API, and Aspire dashboard through the AppHost.
- Give each module its own EF Core DbContext registration, explicit schema mapping, migrations assembly/history table, and initial schema migration.
- Acquire the documented PostgreSQL advisory lock in the Migrator, apply module migrations in declared order, fail nonzero, and make the API wait for completion.
- Add ServiceDefaults, JSON `ILogger` output, OTel/OTLP wiring, liveness/readiness, and graceful shutdown.
- Prove local configuration uses Aspire parameters/user secrets rather than committed credentials.

**Excludes:** Keycloak, Redis, Mailpit, RabbitMQ, Azurite, product-data caching, and business tables invented merely to exercise EF.

**Acceptance:** PostgreSQL and Topology lanes prove empty-database migration, idempotent rerun, advisory-lock exclusion, migration failure blocking API startup, schema/history isolation, health, telemetry, and repeated start/stop without orphaned application processes.

## Slice 2 — Identity and Organization access

### Increment 2.1 — BFF identity session and JIT User link

**Outcome:** a person signs in through local Keycloak, receives a secure server-side session, and is linked to one product User by immutable issuer/subject.

**Work:**

- Add Keycloak and Redis to Aspire with reproducible realm/client configuration and development secrets outside source.
- Implement the BFF authorization-code flow, secure cookie, focused Redis `ITicketStore`, logout/revocation, and Data Protection configuration appropriate to local single-host development.
- Persist Access User and External Identity through an explicit JIT use case; keep tokens and provider types at the API adapter.
- Provide a minimal authenticated identity endpoint sufficient for tests, not a frontend application.

**Acceptance:** PostgreSQL and Topology lanes prove first login/link, repeat login, immutable issuer/subject, email-change tolerance, invalid issuer/audience rejection, Redis failure closed, lost ticket causing logout, and no token/secret telemetry. Most auth tests use local test identities; a focused topology test uses real Keycloak.

### Increment 2.2 — Organization bootstrap and bookmarkable routing

**Outcome:** an authenticated User creates an Organization, becomes its first Organization Administrator, and enters it through `/o/{organizationSlug}`.

**Work:**

- Implement Organization creation, slug normalization/reservation, initial Membership/role assignment, audit, and operation-specific errors in one Access transaction.
- Implement Organization chooser behavior: zero memberships shows onboarding, one redirects directly, multiple lists choices; no authoritative last-Organization session state.
- Resolve the route slug and current Membership into explicit request-scoped Organization/actor context.
- Establish explicit `OrganizationId`, query filters/scoped queries, write validation, and tenant-aware unique indexes for Access data.

**Acceptance:** PostgreSQL and API tests prove slug races, atomic first-admin creation, bookmarkability, one/many membership navigation, suspended/nonmember denial, forged/cross-tenant identifiers, and missing/mismatched `OrganizationId` rejection.

### Increment 2.3 — Invitation creation and email delivery

**Outcome:** an Organization Administrator creates or explicitly resends a single-use invitation, and Access durably delivers the current invitation generation without provisioning an IdP account.

**Work:**

- Add the pending Invitation lifecycle, minimal role selection, expiry, and explicit resend with secret rotation.
- Commit Invitation, audit, and Access-owned email-outbox row together.
- Add Mailpit and a native hosted sender; tolerate ambiguous duplicate email delivery of the same generation.
- Protect the recoverable delivery payload with Data Protection and document the mandatory shared durable production key ring.

**Acceptance:** PostgreSQL/Topology tests prove administrator enforcement, role validation, active-member rejection, one pending invitation per normalized recipient, protected payload storage, resend generation behavior, and SMTP delivery to Mailpit.

### Increment 2.4 — Invitation acceptance and provider admission

**Outcome:** the matching authenticated person accepts one valid invitation without Access provisioning an identity-provider account.

**Work:**

- Resolve the invitation bearer secret without storing it in plaintext and consume it once.
- Require an authenticated/JIT-linked User and a matching verified provider email before atomically creating Membership and role assignments.
- Support both OpenRegistration and DirectoryGated provider admission without provider administration APIs.
- Keep provider tokens and administration SDKs outside Access.

**Acceptance:** PostgreSQL/Topology tests prove expiry, single use, recipient mismatch with an explicit wrong-account retry, concurrent/idempotent acceptance, JIT identity link, open-registration acceptance, a directory-gated login rejection leaving the product invitation pending, and absence of the invitation bearer from exported logs, traces, OIDC state, and referrers.

### Increment 2.5 — Membership administration and product roles

**Outcome:** an Organization Administrator lists members, assigns system roles, suspends/reactivates/removes memberships, and cannot remove the last active administrator.

**Work:**

- Implement the accepted role catalog and stable module permission tokens as reviewed code.
- Persist role assignments by stable textual role ID; do not synchronize role-definition rows at startup.
- Re-check current membership on use so suspension/revocation takes effect without distributed authorization caching.
- Add Access audit for accepted changes and security-significant denials.

**Acceptance:** application/PostgreSQL tests prove last-administrator races, immediate suspension effect, role replacement, organization isolation, Organization Administrator not implying business permissions, and concurrent changes under optimistic/database constraints.

## Slice 3 — Inventory and the constrained event store

### Increment 3.1 — Stock Item and Stocking Location

**Outcome:** an authorized Inventory Manager maintains the minimum reference data required by Sales and Stock Positions.

**Work:**

- Implement state-stored Stock Item and Stocking Location vertical slices, stable customer-provided SKU/location codes, immutable v1 base unit, active state, audit, and tenant-scoped indexes.
- Add one batched Inventory Contracts query that resolves Stock Item references for Sales without exposing persistence types.

**Acceptance:** domain/PostgreSQL/contract tests prove normalization, duplicate races, tenant isolation, inactive-item behavior, immutable base unit after use, batched lookup semantics, permission enforcement, and audit.

### Increment 3.2 — Stock Position append and inline current state

**Outcome:** an Inventory Manager records stock receipt against one Stock Position and reads the atomically updated current state.

**Work:**

- Implement the minimum EF Core-backed event stream/metadata tables inside `InventoryDbContext` with stable event alias/schema version, JSONB payload, event/stream IDs, stream version, recorded time, tenant, and global sequence.
- Implement the Stock Position state/decider wrapper, deterministic evolution, uncommitted events, expected-version append, and inline current projection.
- Keep decision and evolution responsibilities separate. Normal command loading uses the complete inline write model with stream-version verification; explicit live replay uses retained history. Keep stream headers domain-neutral and enforce Stock Position business-key uniqueness through its required write model. Projection-loss repair and writer-safe rebuild remain operational requirements, not an independent identity index.
- Add receipt behavior sufficient to establish positive on-hand stock. Corrections remain in Increment 3.4.

**Acceptance:** domain/PostgreSQL tests prove deterministic hydration, optimistic conflict, unique event IDs, atomic stream/projection rollback, constrained decimals, no cross-tenant stream access, and no assembly-qualified type name as canonical discriminator.

### Increment 3.3 — Temporal reads and event evolution fixtures

**Outcome:** an authorized user can inspect Stock Position state at latest, stream version, or recorded instant without exposing raw persistence as the product interface.

**Work:**

- Define timestamp inclusivity/order semantics and implement latest/version/as-of hydration through the same evolution function.
- Persist compatibility fixtures for every accepted Stock Position event name/version.
- Demonstrate one explicit old-version transformation only if a real schema change is needed; otherwise keep the extension point absent.
- Add a curated Stock Position history representation distinct from raw JSON and security audit.

**Acceptance:** fixed-clock and database integration tests prove inclusive boundary timestamps, same-timestamp stream-version ordering independent of global sequence allocation, historical determinism, fixture compatibility after namespace refactors, and replay without domain/integration event dispatch. Expected query failures remain results; corrupt persisted history and inconsistent required write models raise structured module-local integrity exceptions through the existing generic HTTP 500 handling.

### Increment 3.4 — Correction and projection rebuild proof

**Outcome:** bad stock is corrected by an immutable event, and the current projection can be rebuilt and verified without mutating history or emitting external effects.

**Work:**

- Append a reasoned correction event; never update/delete historical events as the normal correction path.
- Implement one Inventory-specific full reconstruction and atomic replacement operation; defer durable shadow state/checkpoints/resume.
- Keep this concrete; do not introduce a generic async-projection/upcaster framework.

**Acceptance:** PostgreSQL tests inject replacement failure and cancellation, retry from the beginning without partial serving-state changes, compare reconstructed/current state, reject corrupt/unknown events clearly, and prove historical events/business audits are not repeated. Each successful explicit rebuild has its own operational audit.

**Review-sized delivery:**

- **3.4a — Reasoned quantity corrections:** command/HTTP ingress, immutable correction event, quantity invariants, curated history and fixture, authorization, conflicts and transaction rollback. No rebuild machinery in this change set.
- **3.4b — Administrative rebuild proof:** Inventory-owned single-call full replay/atomic replacement contract, cancellation/failure rollback and fresh-scope retry from the beginning, writer coordination and current-model comparison. No durable job or rebuild migration. PostgreSQL fault injection is setup; acceptance is observed through the administrative contract and normal Stock Position reads.

### Post-3.4 event-sourcing seam review

Before durable messaging builds on Stock Position, review the implemented load-for-writing,
event identity/codec, temporal hydration, metadata, inline projection, and rebuild mechanics.
Identify concrete duplication and substitution needs; retain Inventory business policy and
projection definitions in Inventory. Record candidates now, but defer extraction to the explicit
second-aggregate validation and extraction increments in Slice 8. The second aggregate must have
coherent domain behavior and demonstrate distinct state/projection needs, not be a renamed clone.

## Slice 4 — Sales order and approval

### Increment 4.1 — Customer and draft Sales Order

**Outcome:** a Sales Clerk creates the minimum Customer and a draft order whose lines contain validated immutable Inventory snapshots.

**Work:**

- Implement minimal Customer creation and draft Sales Order creation by vertical slice.
- Batch-resolve Stock Item references through Inventory.Contracts and store stable identity, SKU, description, base unit, quantity, unit price, and one order currency.
- Assign a short Organization-scoped order number with gaps allowed.

**Acceptance:** domain/application/PostgreSQL/contract tests prove positive quantity, monetary rules, inactive/foreign/missing item rejection, one batched lookup, immutable snapshots, number-race handling, tenant isolation, permissions, and audit.

**Review-sized delivery:**

- **4.1a — Customer creation and lookup:** minimal state-stored Customer, organization-unique stable user-supplied code, Sales-owned Contracts/authorization/audit/persistence, and authenticated organization-scoped HTTP adapters. PostgreSQL tests cover validation, uniqueness/concurrent creation, tenancy, permission/context denial and audit rollback; the topology lane covers the HTTP path, CSRF and current role revocation. No draft order or Inventory dependency in this checkpoint.
- **4.1b — Draft Sales Order:** aggregate-owned lines, one batched Inventory contract lookup, immutable line snapshots, quantity/money/currency rules and short organization-scoped order numbering. No submit/approve transition or broker yet.

### Increment 4.2 — Submit, approval authority, and activity

**Outcome:** a Sales Clerk submits a valid order and a different authorized member approves it within current Sales-owned authority.

**Work:**

- Add Sales-managed per-Membership Approval Authority plus submit/approve transitions and operation-specific failures.
- Evaluate authority-management permission, current Membership, amount/currency, order state, and separation of duties in the owning handler transaction.
- Add curated order activity plus separate audit entries for accepted and denied decisions.
- Create initial Order Fulfilment Process state on approval, but do not add RabbitMQ until the next slice.

**Acceptance:** tests prove authority assignment isolation, unauthorized authority changes, invalid state transitions, same-person denial, changed/suspended membership, limit/currency failure, concurrent approvals, audit of denials without leaking sensitive payloads, and atomic approved-order/process creation.

**Review-sized delivery:**

- **4.2a — Submission and curated activity:** transition a draft to awaiting approval through an explicit command; retain submitter and submission time; use an explicit expected order version and EF optimistic concurrency; commit the transition, activity and success audit together. Expose an organization-scoped, permission-checked activity query rather than raw audit/event payloads. PostgreSQL proofs cover invalid transitions, stale/concurrent commands, actor/tenant/permission denial and rollback; the topology lane covers the authenticated HTTP path. No authority, approval, fulfilment process or broker in this checkpoint.
- **4.2b — Sales Approval Authority:** manage and query Sales-owned amount/currency authority for a particular Membership tenure; enforce Sales management permission and current organization membership through Access Contracts. Prove organization isolation, validation, concurrent changes and atomic authority/audit persistence. No order approval in this checkpoint.
- **4.2c — Approval and initial fulfilment state:** approve an awaiting order with current authorization and Sales authority, separation of duties and concurrency enforcement. Commit the approval, curated activity, audit and initial concrete fulfilment process state atomically. No broker until Slice 5.

## Slice 5 — Durable fulfilment

### Increment 5.1 — Inventory durable reservation endpoint

**Outcome:** Inventory consumes an at-least-once `ReserveStock` command and durably publishes exactly one semantic reservation outcome for a business operation.

**Work:**

- Add RabbitMQ to Aspire and the isolated Inventory `AddRebusService` endpoint, stable input/error queue, bounded workers/prefetch/retries, and explicit subscriptions.
- Add Inventory-owned receiver command and outcome-event contracts without Rebus types.
- Implement Inventory-local inbox/outbox rows and hosted relay with leasing/recovery; commit inbox, event append, inline projection, audit, and outcome outbox atomically.
- Use a test publisher/subscriber at the broker seam; do not create fake production modules.

**Acceptance:** PostgreSQL/Topology/Broker tests prove full-line reserve/shortage, duplicate message ID, repeated business-operation ID under a new message ID, conflicting operation reuse, concurrent reservation, handler crash before/after commit, relay crash after publish, redelivery, and Inventory-only poison/error routing.

**Review-sized delivery:**

- **5.1a — Receiver and failure windows:** real RabbitMQ/Rebus composition, module-owned inbox/semantic-operation/outbox persistence, atomic event-source reservation, publisher-confirmed relay, broker-seam tests and a CI lane. Inject failures before commit, after commit before ACK, and after publish before dispatch marking; verify rollback, redelivery and expired-lease recovery. These deterministic probes are not abrupt process termination.
- **5.1b — Abrupt restart proof:** run the actual receiver in a disposable process, terminate at controlled pre/post-commit and post-publish boundaries, restart against retained PostgreSQL/RabbitMQ and prove one stock effect with stable outgoing identity. Keep this acceptance item open rather than substituting graceful restart or injected exceptions. Replica isolation/shutdown/reconciliation remain in the broader 5.6 matrix.

**5.1b delivery evidence:** `InventoryCrashRecoveryTests` launches the test-only `BrokerReceiver` executable through `dotnet exec`, using the production Inventory composition. PostgreSQL barriers identify the pre-commit and dispatch-mark windows; a test-only receive pipeline checkpoint identifies post-commit/pre-ACK. Each case forcibly terminates its owned child and restarts with retained infrastructure. Outbox recovery waits for the actual 30-second lease rather than modifying it. No production fault hooks or extracted messaging framework are introduced.

### Increment 5.2 — Sales Order Fulfilment Process round trip

**Outcome:** approving an order durably sends one reservation command per line and Sales records independent reservation/shortage outcomes until the process reaches its correct aggregate status.

**Work:**

- Add isolated Sales endpoint, Sales-local inbox/outbox, relay, concrete process state, per-line operation IDs, attempts/deadlines, and Inventory outcome handlers.
- Have approval atomically create process state and outbox commands; do not publish from an in-memory post-commit handler.
- Start any existing `pending-dispatch` processes from Increment 4.2c through the same idempotent command-enqueue path; introducing the broker must not strand already-approved orders.
- Keep process transitions explicit in Sales application code and persist them with inbox, audit/activity, deadlines, and outgoing messages.
- Compare and record the two real inbox/outbox implementations. Any justified extraction of shared EF/Rebus mechanics is a separate review-sized increment after the round trip is proven, never bundled into the feature increment; apply the deletion test before adding a library.

**Acceptance:** multi-line tests prove all-reserved, mixed reserved/shortage, out-of-order/duplicate/concurrent outcomes, restart, stale/foreign tenant messages, no hidden API workflow, and correct process/activity state.

### Increment 5.3 — Purchasing Stock Item snapshot plus tail

**Outcome:** Purchasing begins consuming Stock Item references after Inventory already contains data and reaches a complete, reconcilable local projection without private-stream replay.

**Work:**

- Add the isolated Purchasing endpoint.
- Introduce Inventory's versioned Stock Item snapshot export with a consistent high watermark and `StockItemReferenceChanged` integration event.
- Bootstrap the Purchasing-owned reference projection, buffer/apply tail events after the watermark, checkpoint progress, restart idempotently, and reconcile against Inventory.
- Exercise a test-only separate-worker cutover against the same stable module queue: stop the monolith endpoint before the worker takes ownership. Do not ship a second production deployable in v1.
- Document which synchronous dependencies would still need redesign before actual Inventory or Purchasing extraction.

**Acceptance:** PostgreSQL/Broker tests start from pre-existing Inventory data, mutate concurrently with snapshot, crash/restart at each phase, prove no gap or duplicate semantic effect, detect deliberate drift, and cut over the endpoint without double-applying a business operation.

**Review-sized delivery:**

- **5.3a — Snapshot and live tail:** committed-prefix Inventory reference feed/outbox, consistent versioned snapshot export, isolated Purchasing subscription, atomic checkpoint installation with durable coalesced full-state rows, live idempotent updates and ordinary restart proof. No replenishment or generic bootstrap library. The [concrete protocol and limits](stock-item-bootstrap.md) record transaction/lock ownership, privileged Contracts and scale/recovery assumptions.
- **5.3b — Recovery and extraction rehearsal:** abrupt failure/phase rollback coverage, poison/redrive, drift detection/reconciliation and test-only separate-worker cutover on the same stable queue. Stop the in-process endpoint before cutover. This closes the remaining full 5.3 acceptance matrix; 5.3a alone does not claim it.

**5.3b proof surface:** `StockItemProjectionRecoveryTests` force-kill the production-composed test child during export, installation and receiver commit/ACK windows; exercise retained-queue cutover, poison/redrive and explicit comparison/repair. Purchasing's administrative reconciliation Contract replaces only snapshot-covered projection state and atomically advances the installed boundary while preserving newer tail rows. The [protocol/runbook](stock-item-bootstrap.md) distinguishes covered comparison from zero-lag health, documents missed-tail repair and source-restore limits, and states that the test worker still needs the in-process Inventory exporter. No remote export adapter, second production deployment, generic repair job or shared library is implied.

### Increment 5.4 — Purchasing Replenishment Requirement

**Outcome:** a shortage causes one Purchasing-owned Replenishment Requirement using its current Stock Item reference, and Sales records creation.

**Work:**

- Add the Purchasing-owned `CreateReplenishmentRequirement` command.
- Have the Sales process send the command after a shortage; validate its Stock Item against the Purchasing projection, then atomically create the requirement/inbox/audit/outbox and publish the created event.
- Add minimal authorized requirement query/list behavior.

**Acceptance:** PostgreSQL/Topology/Broker tests prove duplicate/conflicting shortage operation behavior, missing/stale/foreign Stock Item reference handling, tenant/base-unit validation, Purchasing-only error routing, created-event redelivery, Sales process update, and no auto-created Purchase Order.

**Review-sized delivery:**

- **5.4a — Purchasing receiver:** receiver-owned command, durable pending request/recovery, requirement/inbox/audit/outbox atomicity, outgoing created topic and authorized get/list routes. It works independently of the sample's Sales producer. The [protocol and limits](replenishment-reliability.md) distinguish unavailable/stale references from permanent rejection.
- **5.4b — Sales round trip:** send the real shortage command, handle created/rejected outcomes with correlation and idempotency, recover existing shortage processes, and complete authenticated journey/redelivery proofs. Sales retains separate reservation and replenishment outcomes; requirement creation does not change stock or complete fulfilment. The [protocol and limits](replenishment-reliability.md) record sample deficit policy, atomic participants, retained identities and the current zero-minimum reference freshness boundary.

### Increment 5.5 — Cancellation and Reservation Release compensation

**Outcome:** cancelling an eligible order durably and idempotently releases every successful reservation and exposes compensation progress.

**Work:**

- Make cancellation update Sales order/process/activity/audit and enqueue stable release commands atomically.
- Implement Inventory release idempotency and release-outcome event; handle outcomes in Sales.
- Define the visible states for cancellation requested, compensation pending, compensated, and operator attention after exhausted technical retry.

**Acceptance:** tests cover cancellation before/while/after reservation outcomes, duplicate cancellation/release, release of already released reservation, late success racing cancellation, process death at each commit boundary, and eventual compensated state without negative/resurrected stock.

Deliver as two reviewable increments:

- **5.5a — Inventory release receiver:** directed release command and outcome topic, original-reservation correlation and quantity, retained release operation identity, private release event and replay, atomic rollback/redrive, duplicate/competing releases and receiver crash boundaries. No user-facing release route or Sales cancellation yet. See [protocol and proof limits](reservation-release.md).
- **5.5b — Sales cancellation:** cancellation command, durable per-line compensation progress, release dispatch/outcome handling, cancellation before/while/after reservation and replenishment outcomes, late-success races and eventual compensated/operator-attention states. Wait for a known reservation outcome before sending release; unknown reservation is not successful compensation.

### Increment 5.6 — Durability and operator proof closure

**Outcome:** the complete workflow has repeatable failure evidence and enough operational surface to diagnose stuck work without a generic operations framework.

**Work:**

- Complete the broker failure matrix across all three module endpoints and two application replicas.
- Add narrowly scoped health/metrics/logs for relay age/depth, inbox duplicates, process age, retry/error queue, and projection failures.
- Add documented recovery procedures for retrying/reconciling a process and inspecting named error queues; destructive replay requires explicit operator action and idempotency proof.

**Acceptance:** Broker/Topology tests prove module failure isolation, replica competing-consumer behavior, graceful shutdown, stale lease recovery, bounded poison behavior, and reconciliation after ambiguous outcomes. Telemetry contains identifiers but no tokens or full event payloads.

Deliver through review-sized proof increments rather than a generic operations framework:

- **5.6a — Competing receiver replicas:** run two independently hosted receiver processes on each module's existing input queue. Prove duplicate/semantic-operation delivery while the first replica holds a committed delivery before ACK, then terminate it and prove the surviving replica settles redelivery without repeating the business effect. Assert through Inventory/Sales/Purchasing Contracts and real broker outcomes. This proves endpoint replicas, not two complete API deployments or a globally bounded technical-retry counter.
- **5.6b — Operational visibility and recovery:** add the narrowly justified diagnostics and operator procedures/capabilities for stuck intent and ambiguous results; preserve original operation identities and require explicit redrive. Do not equate an expired deadline with a failed remote effect.
- **5.6c — Remaining failure-matrix closure:** close graceful shutdown, full-application replica/isolation, infrastructure outage, cancellation-ingress death and lease/retry gaps against the actual implementation. Record tested bounds and any accepted limits instead of claiming unproven exactly-once delivery.

The [durability evidence ledger](broker-durability.md) separates 5.6a's committed-delivery failover proof from the still-open operational and full-application failure windows.

Operational work is also delivered vertically. **5.6b1** adds the [retained Inventory outcome recovery](message-delivery-recovery.md): authorized metadata inspection and audited republication through the existing relay, proven against a real Sales compensation gap. It adds no public recovery route or generic message replay tool. **5.6b2** retains telemetry, broader publisher/destination recovery procedures and any justified production maintenance adapter; 5.6b is not closed by the first Contract alone.

**5.6b2a — Messaging signals** adds [outbox observations and receipt/failure counters](messaging-observability.md) through native .NET metrics and safe module logs. Database sampling is independent of publication and metric callbacks, has explicit freshness, and exposes no tenant/message metric labels. Process/projection diagnostics, broker error-queue monitoring and any justified maintenance adapter remain the next operational increment; none is implied by a healthy or empty outbox.

The **5.6b2a export follow-up** extends the existing two-lifecycle Topology proof to require all three module meter/backlog instrument names at the API's configured OTLP receiver. Fresh receivers prevent stale-run evidence; the faster export interval is test-only. This proves host/exporter wiring, not production telemetry-backend retention or numeric metric semantics, and does not close the remaining operational work.

**5.6bc1 — Workflow observations and fault isolation** pairs [Sales process diagnostics](workflow-operations.md) with damaged-process isolation, independent stale-source handling, real broker stop/start recovery and orderly pending-work shutdown. It is the first self-contained checkpoint of the combined remaining 5.6b/c workstream, not closure of either increment; projection/broker operations and full-application/in-flight failure windows remain explicit.

## Slice 6 — Minimal frontend journey

### Increment 6.1 — Vite/BFF shell and Organization navigation

**Outcome:** the browser signs in through the BFF, chooses or creates an Organization, and retains bookmarkable Organization routes without holding tokens.

**Work:**

- Select the smallest suitable frontend framework at this increment, record the choice, and add Vite with directly pinned pnpm, lockfile, Prettier, commitlint, and Lefthook integration.
- Add only the BFF shell, login/logout, Organization chooser/onboarding, canonical routes, accessible error handling, and no client-side token storage.
- Add one Playwright smoke path because real browser behavior now exists.

**Acceptance:** Browser/Topology tests prove login, one/many Organization navigation, direct bookmark reload, unauthorized slug denial, logout, cookie attributes, and absence of browser tokens.

### Increment 6.2 — Critical wholesale workflow UI

**Outcome:** users can exercise the reference workflow through minimal screens rather than a comprehensive ERP frontend.

**Work:**

- Add the smallest screens for Stock Item/Location/receipt setup, Customer/order creation, submit/approve under separate users, fulfilment status, shortage requirement, cancellation, and curated activity.
- Keep administration UI limited to invitation/role steps required to establish the two-user scenario.
- Apply BFF aggregation only where a concrete screen otherwise requires awkward chatty calls; business workflows remain in modules.

**Acceptance:** one Browser smoke journey covers setup through approved order and reservation/shortage observation; a second focused path covers cancellation/release. HTTP/application tests retain most behavioral coverage.

## Slice 7 — Packaging and Azure private pilot

### Increment 7.1 — OCI image and environment contract

**Outcome:** the application image and separately runnable finite migration artifact/job use versioned non-root OCI packaging and only documented environment inputs.

**Work:**

- Publish the application image and separately runnable migration artifact/job, generate dependency inventory/SBOM, tie versions to the source revision, and define graceful shutdown/probes/resources.
- Run the same image locally against externalized PostgreSQL/Redis/RabbitMQ/Keycloak-style configuration without Kubernetes manifests.
- Document secrets, Data Protection, broker, telemetry, database, and identity environment contracts.

**Acceptance:** clean-image smoke applies migrations, starts healthy, completes one workflow, shuts down gracefully, rejects missing required configuration, and emits expected telemetry as a non-root process.

### Increment 7.2 — Cheapest feasible Azure private pilot

**Outcome:** a restricted private pilot deploys the one application to Azure Container Apps with managed dependencies and explicit spend ceilings.

**Work:**

- Provision/deploy Container Apps Consumption, a finite migration job, PostgreSQL Flexible Server pilot SKU, Service Bus Standard, Azure Managed Redis pilot SKU, ACR Basic, Key Vault, Entra External ID, Blob-backed Data Protection keys where required, and bounded Azure Monitor/Application Insights.
- Use managed identity where supported and keep Key Vault out of local development.
- Set application/edge rate limits, body/concurrency limits, maximum replicas, database/broker worker limits, telemetry sampling/daily cap, budgets/anomaly alerts, and a documented kill switch.
- Keep the pilot restricted; gateway/WAF/private-origin production topology remains a later threat-model decision.

**Acceptance:** Azure compatibility tests prove login/logout, migration-before-start, one durable workflow, Service Bus command/fan-out/error behavior, secret access without embedded credentials, and scale ceilings. Recreate the current pricing estimate before deployment.

### Increment 7.3 — Recovery and release evidence

**Outcome:** the pilot can be restored, rolled back, and diagnosed rather than merely deployed successfully once.

**Work:**

- Perform PostgreSQL point-in-time/backup restore into a clean target, application rollback with compatible migrations/messages/events, and broker/process reconciliation.
- Record RPO/RTO observations, single-point-of-failure limitations, runbooks, dashboard queries, cost, and the explicit promotion gate for HA/public production.
- Run the real Service Bus Standard and Entra compatibility suites in their protected CI environment.

**Acceptance:** deployment smoke passes after restore and rollback; the prior release can run against the retained schema/contracts; cost and security limitations are visible and no AKS/Flux claim is made.

## Slice 8 — Reference-to-product handoff

### Increment 8.1 — Second event-sourced aggregate proof

**Outcome:** another concrete aggregate, preferably in a different module, tests whether the Inventory event-sourcing seams work outside Stock Position.

**Work:**

- Select the aggregate and update its owning charter at this increment; do not silently convert Sales/Access/Purchasing models during earlier slices.
- Implement real decisions, a distinct decision-state shape, an aggregate-shaped inline write model, and an additional justified inline view through explicit C#/EF code.
- Repeat the hydration/evolution, tenant isolation, event compatibility, concurrent append, and multi-projection rollback proofs.
- Identify duplication in load/append, serialization/upcasting, metadata, projection coordination, and recorded-time hydration before choosing abstractions.

**Acceptance:** both aggregate implementations work with their own module schema/transactions and no copied Inventory policy; failure tests demonstrate atomic updates of all required inline views. This is a planned capability proof, not a reason to event-source every aggregate.

### Increment 8.1b — Event-sourcing correctness and extraction gate

**Outcome:** core event-sourcing limits are resolved with tests or explicitly constrain the supported library before extraction.

**Work:**

- Revisit projection-dependent business-key identity and the missing-lookup/expected-version-zero reopening risk.
- Measure full replay and Organization-wide repair blocking; tighten coordination only with tested stream discovery/creation and lock ordering.
- Validate shared-reducer semantics with independently expected fixtures, not just equivalence between two uses of the same code.
- Prove owning-module event/inline-view/audit/inbox/outbox atomicity for actual durable workflows.
- Consider resumable shadow reconstruction only if recovery cost justifies it; adoption requires compatible versioned checkpoints and atomic progress. Otherwise retain full reconstruction with explicit operational limits.

**Acceptance:** the [core-correctness register](event-sourcing.md#core-correctness-follow-up-register) has evidence-backed resolutions or clearly documented supported limits. No generic abstraction may conceal an unresolved correctness gap. Split independent fixes into review-sized changes when necessary.

### Increment 8.2 — Focused library and sample separation

**Outcome:** the two implementations identify reusable event-sourcing infrastructure; reference business behavior remains a sample.

**Work:**

- Extract only the technical mechanics concretely shared by both implementations; retain domain deciders/reducers, projection definitions, authorization, and transaction ownership in their modules.
- Event sourcing is a separate opt-in library. State-stored modules require neither its packages nor its aggregate base types/runtime registrations. No business-module Contracts dependency is allowed in that library; this is a delivery requirement, not a reason to invent generic mechanics before the second concrete proof.
- Review explicit projector registration/preview/rebuild seams without introducing code generation, generic repositories, or automatic domain-to-integration-event dispatch.
- Keep async projections recorded as later work rather than implementing a generic daemon as part of extraction.

**Acceptance:** both real implementations pass their unchanged behavioral proofs using the extracted mechanics; the library has no Inventory-specific fields, schema names, or module Contracts dependency. If a proposed abstraction does not remove meaningful duplication, omit it.

### Increment 8.3 — Configured scaffolding and product rehearsal

**Outcome:** the reference implementation can produce a second repository without depending on a reusable foundry runtime.

**Work:**

- Write and execute a minimal configuration-driven scaffolding process plus copy/rename or agent-assisted checklist against a concrete second repository selected at that time. Product/namespace naming is the initial configuration; additional options require an exercised implementation and compatibility tests.
- Replace example product/module/domain names as required, remove reference-only behavior, retain validated structural conventions, and scan for old namespaces/schema names/event aliases/queue names.
- Build, test, start, migrate, and deploy the second repository through the same path.
- A bounded configuration script is sufficient. Choose an interactive wizard only if the actual consumer workflow needs it; do not build a generic extensible generator or untested provider/persistence matrix.

**Acceptance:** the second repository passes its own CI, starts locally, and deploys without a runtime dependency on `modulith-foundry`. Persisted identifiers that must remain compatible are deliberately mapped rather than blindly renamed.

## Infrastructure introduction order

| Resource/tool | First increment | Reason |
| --- | --- | --- |
| PostgreSQL | 1.2 | Module persistence and migration proof |
| Keycloak + Redis | 2.1 | Real BFF identity/session behavior |
| Mailpit | 2.3 | Actual invitation email effect |
| RabbitMQ + Rebus | 5.1 | Actual durable reservation command and outcome |
| pnpm + Vite | 6.1 | First browser interface |
| Azure managed services | 7.2 | First Azure pilot |
| Azurite | Deferred | No accepted attachment operation; local Data Protection does not justify an always-on Blob emulator |
| k3s/AKS/Flux | Deferred | No v1 operational driver after Container Apps pilot selection |

## Explicit decision points during implementation

These do not reopen Phase 0 by default:

- The exact C# type/method names and folder layout are chosen inside the owning increment while preserving the chartered interface.
- Extract shared inbox/outbox mechanics only after Sales and Inventory implementations demonstrate the same code and tests.
- Choose the frontend framework in Increment 6.1 against the tiny accepted UI; Vite and pnpm are already fixed.
- Choose the Azure infrastructure-definition mechanism before Increment 7.2 and review its license/lifecycle then; the target topology is already fixed.
- If implementation evidence makes an accepted design awkward or unsafe, stop, update the relevant plan/ADR/tests, and obtain approval for the revised change set before proceeding.

## Phase 0 exit checklist

- [x] Prior-attempt failures have explicit countermeasures and proof locations.
- [x] Product purpose, first workflow, invariants, and four module charters are written.
- [x] Transaction, event sourcing, audit, messaging, identity, authorization, tenancy, testing, and deployment directions are decided or explicitly deferred.
- [x] Dependency versions/licenses have primary-source evidence and an implementation-time recheck rule.
- [x] V1 work is ordered into review-sized, independently verifiable increments.
- Repository-owner approval is an external gate recorded in review and commit history, not a mutable checkbox in this file.

The approval gate was satisfied before Increment 1.1 began; later increments still require their own authorized scope and exact-change-set commit approval.
