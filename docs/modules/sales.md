# Sales module charter

## Purpose

Sales captures customer demand, decides when a Sales Order is valid and approved, and owns the durable Order Fulfilment Process until the order is reserved, waiting on replenishment, cancelled, or complete.

## Owned concepts and data

- `Customer`.
- `Sales Order`, its lines, status, immutable submitted item snapshots, monetary totals, submitter, and approver.
- `Sales Approval Authority` and Sales-owned approval decisions.
- `Order Fulfilment Process`, per-line outcomes, compensation state, deadlines, and curated order activity.
- Sales-schema audit, inbox, outbox, and process records.

The `sales` PostgreSQL schema is authoritative. Sales stores the Stock Item identity and immutable SKU/description/base-unit snapshot needed to understand an order line; it never reads Inventory tables.

Sales Orders, their fulfilment outcome, and compact completed-process/business-operation tombstones are retained for the life of the business record in v1. Inbox/outbox operational payloads follow the bounded windows in the architecture plan; compliance retention and subject-data handling remain explicit later decisions.

## Interface

User-facing commands:

- create and maintain the minimum Customer record used by the reference workflow;
- create a draft Sales Order with one or more lines;
- submit a valid draft for approval;
- set a Membership's Sales Approval Authority as an authorized Sales Manager;
- approve an awaiting order within the actor's Sales Approval Authority;
- cancel an eligible order with a reason.

Queries:

- get a Customer by its Organization-scoped stable code;
- get an Organization-scoped Sales Order by its short business order number;
- list the limited order set needed by the exercise UI;
- get fulfilment status and a curated activity timeline.

The order-creation use case performs one batched Inventory contract query to validate Stock Item identities and capture their display/base-unit snapshot. It does not retain an `IQueryable`, Inventory entity, or DbContext.

## Draft-order proof boundary

- `ISalesOrderOperations` creates a draft for a Customer code and retrieves it by short Organization-scoped order number. HTTP ingress is `POST /api/o/{organizationSlug}/sales/orders` and `GET /api/o/{organizationSlug}/sales/orders/{orderNumber}`.
- A draft has 1–100 aggregate-owned lines. Each retains the Inventory-owned Stock Item ID and an immutable SKU/description/base-unit snapshot captured at creation. Repeated items remain separate lines; one batch lookup resolves distinct item IDs. Missing/foreign references are reported as missing; inactive references cannot create new lines.
- Quantity is positive with at most six fractional digits; unit price is non-negative with at most four. The sample supports USD/EUR only. Each line amount rounds quantity × price to two decimals, midpoint away from zero; the total sums those rounded line amounts. PostgreSQL numeric ranges are validated before persistence. Zero-price lines are valid; tax, discounts, pricing engines, other currency conventions and conversion are not implemented.
- Customer, actor/context and current Sales permission checks precede persistence. Creation requires `sales.orders.create`; lookup requires `sales.orders.view`. Stock validation is a point-in-time in-process query, not a cross-module atomicity guarantee; Inventory remains responsible for later reservation eligibility.
- Sales allocates an Organization-local positive number with one atomic PostgreSQL counter upsert in a separate committed connection. Gaps after validation, failed persistence or cancellation are allowed. Number uniqueness is also constrained on the order table. Order, owned lines and success audit commit together in the owning Sales `SaveChanges` transaction; the counter is intentionally outside it.
- A creation retry is a new order attempt, not a deduplicated business operation. No order list, editing, lifecycle/status transitions, approval, fulfilment, broker or event sourcing is added by 4.1b. Those capabilities remain in their subsequent increments.

## Submission and activity proof boundary

- `ISalesOrderOperations.SubmitAsync` transitions only a draft to `AwaitingApproval`, retaining the verified product User as submitter and a UTC submission timestamp. The timestamp uses PostgreSQL microsecond precision so the command response and subsequent reads agree. Creation starts at version 1; submission advances to version 2. The version is an EF optimistic-concurrency token, not an event-stream version.
- `POST /api/o/{organizationSlug}/sales/orders/{orderNumber}/submit` requires BFF antiforgery and a positive `expectedVersion`. Current actor/Organization context and `sales.orders.submit` are checked before reading/mutating the order. Missing orders return 404; invalid versions return 400; stale/concurrent commands and non-draft transitions return 409. A lost-response retry does not submit twice: reload the order to discover its state, rather than automatically retrying a conflict.
- Submission, its curated activity and success audit commit in one Sales `SaveChanges` transaction. A concurrent loser rolls back and clears its tracked candidate changes before returning a conflict; arbitrary persistence faults propagate to the existing generic error handling. Operations own their persistence boundary; callers must not stage unrelated Sales changes for a command to commit. Use a fresh operation scope after an unexpected persistence failure.
- `GET /api/o/{organizationSlug}/sales/orders/{orderNumber}/activity` requires current `sales.orders.view` permission and returns only kind, actor User ID, order version and occurred time, ordered by order version. Created/submitted activity records are separate tenant-owned read records, not raw audit payloads, domain-event dispatch or event sourcing. Security-significant permission denials are audited but never become customer-facing order activity; validation/state/version rejections are expected results, not security audit events.
- Newly created orders record creation activity atomically. The additive migration gives older drafts status `draft` and version 1, but does not invent creation activity with an unknown historical actor. Existing item snapshots and amounts are not refreshed on submission. Stock availability remains a later Inventory reservation decision.
- Request authorization is checked through Access Contracts when the operation executes; this increment does not claim atomicity with a simultaneous Access revocation. Approval's stricter authority/commit-time requirements remain a separate 4.2c proof. No editing, approval authority, approval, fulfilment process, broker, generic result mapper or domain-event framework is added here.

## Approval-authority management proof boundary

- `ISalesApprovalAuthorityAdministration` sets, queries and revokes a Sales-owned Approval Authority for one Organization/Membership identity. HTTP ingress is `PUT`/`GET /api/o/{organizationSlug}/sales/approval-authorities/{membershipId}` and `POST .../{membershipId}/revoke`; both writes require BFF antiforgery. Every operation checks verified actor/Organization context and current `sales.approval-authorities.manage` permission. Organization Administrator alone and Sales Approver do not grant this permission.
- Set requires an active target Membership in that same Organization, checked through `IOrganizationMembershipQueries.IsActiveAsync`. That query returns no personal data and requires no access-administration permission, so a Sales Manager can use it without being an Organization Administrator. Its Access query remains tenant-filtered; no Access entity or table is exposed to Sales. A missing, foreign, suspended or removed target cannot receive authority.
- The sample has one currency and maximum amount per Membership tenure, supporting USD/EUR, nonnegative `decimal(19,2)` amounts and zero limits. It does not implement currency conversion, multiple independent currency limits or policy inheritance. Authority is necessary but not sufficient for approval: the later approval use case also requires current permission, membership, order state and separation of duties.
- Set uses expected version zero only for initial creation; replacement and re-enablement require the retained current version. Every accepted Set advances the version, including replacement with the same values. Revoke requires a positive current version and retains a disabled row rather than deleting it. Repeating Revoke at that disabled version is unchanged and adds no success audit; stale requests still conflict. A manager may revoke authority for an ended/suspended tenure. A new Membership after removal inherits no authority.
- Versions are EF optimistic-concurrency tokens. A unique Organization/Membership index arbitrates competing first grants; only that named unique violation is classified as a version conflict. Authority changes and success audit commit together in one Sales `SaveChanges` transaction. Expected concurrent losers clear staged changes; unexpected storage faults propagate to generic error handling and require a fresh operation scope. Security-significant permission denials are audited without a target identity or request payload; validation, missing-target and version rejections are expected results.
- Membership and permission queries are point-in-time checks, not an atomic guarantee against simultaneous Access changes. This increment does not approve orders or claim to solve approval's commit-time requirements; those remain the 4.2c proof. No broker, process state, generic authorization pipeline, cross-module transaction or reusable approval framework is added.

## Durable collaboration

Sales owns orchestration. Its outbox sends Inventory-owned `ReserveStock` and `ReleaseReservation` integration commands and, after a shortage, a Purchasing-owned `CreateReplenishmentRequirement` command. Sales consumes Inventory reservation-outcome events and Purchasing's requirement-created event through its module endpoint.

No Sales integration event is published in v1 without an actual consumer. Starting fulfilment is an explicit Sales transaction that creates process state and outgoing outbox records with the approved order; it is not a recursive domain-event cascade.

## Invariants

- A Customer has one immutable user-supplied code unique within its Organization and a required display name. The reference creation flow canonicalizes codes to uppercase ASCII (1–64 letters/digits, hyphens, underscores or periods; first character alphanumeric), trims names (1–200 characters) and rejects name control characters. Codes identify HTTP resources; customer names need not be unique.
- Customer creation and its success audit commit together. Current actor/Organization context and `sales.customers.manage` are required for creation and administrative lookup; Organization Administrator alone does not grant Sales permissions. The v1 Customer proof deliberately excludes addresses, contacts, editing and lifecycle operations.
- A draft belongs to one Organization and Customer and contains at least one line before submission.
- Each line has a positive quantity and money expressed in the order's single currency.
- Submitted line snapshots do not change when an Inventory description changes later.
- Only an awaiting order may be approved; the submitter cannot approve the same order.
- The approver must hold the permission and satisfy Sales-owned amount/currency authority at commit time.
- Fulfilment is all-or-nothing per order line but may be partially successful across lines.
- Each line reaches one current reservation outcome for a process attempt; duplicate messages do not duplicate effects.
- Cancellation is idempotent, records a reason, and durably requests release of every successful reservation before the process becomes fully compensated.

## Authorization

Sales exposes the stable `sales-clerk`, `sales-manager`, and `sales-approver` role identifiers from Sales.Contracts. It defines and enforces permissions such as `sales.customers.manage`, `sales.orders.create`, `sales.orders.submit`, `sales.approval-authorities.manage`, `sales.orders.approve`, `sales.orders.cancel`, and `sales.orders.view`. HTTP policy checks are coarse adapters; Sales handlers re-check organization, resource state, permission, separation of duties, and approval authority for every entry path.

## Explicit exclusions

- Pricing engines, tax, discounts, invoicing, payment, shipping, returns, and complete order-to-cash behavior.
- Reading or writing Inventory/Purchasing persistence.
- A generic workflow DSL, generic saga base, mediator, or API-owned orchestration.
- Atomic reservation of all order lines or a shared multi-stream transaction abstraction.
