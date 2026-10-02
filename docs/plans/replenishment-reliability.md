# Purchasing replenishment reliability

## Receiver-owned protocol

`CreateReplenishmentRequirementV1` is a directed Sales workflow command, not a browser capability. Purchasing checks producer, delivery identity and process correlation in its Rebus adapter. Headers are consistency checks, not credentials; broker access remains a trusted infrastructure boundary.

The payload identifies one Organization, process/order/line, Stock Item, quantity/base unit and stable shortage Operation ID. Its operation fingerprint includes all semantic fields, including the optional minimum reference revision; delivery identity additionally includes Message ID and creation time. Repeated identical intent creates no second requirement or outgoing message. Conflicting intent is audited once per conflicting delivery and remains poison on subsequent retries. Malformed payloads, mismatched transport metadata and conflicting delivery identity reach Purchasing's error queue.

Purchasing stores a compact request record while reference data is unavailable. This is durable intent/business-operation identity, not a broker lease or generic saga. Unknown references, an uninitialized projection or a reference below the requested minimum revision remain pending. A native worker scans bounded batches and retries in fresh scopes; restart needs no redelivery of an already acknowledged pending request.

Minimum revision is an observed revision of this Stock Item, not the catalog's snapshot watermark. Zero means no caller-supplied lower bound. It does not prove the projection is caught up with every source write. Eligibility is checked only after satisfying a nonzero lower bound.

A known foreign-Organization Stock Item, immutable base-unit mismatch or inactive reference settles the request as rejected without a requirement and stages `ReplenishmentRequestRejectedV1`. These are diagnostic business rejections, not infinite technical retries. Request queries are trusted workflow/administrative Contracts, not HTTP APIs. Malformed or conflicting intent remains poison rather than a fabricated business outcome.

## Sales-owned round trip

Sales queues `CreateReplenishmentRequirementV1` in the same explicit transaction that records Inventory's shortage outcome, inbox receipt, activity and audit. The sample requests the observed deficit: requested 100 and available 10 produces a requirement for 90. This is sample policy, not a general procurement calculation or reservation of those available units.

Each shortage line retains its operation identity, a separate replenishment command Message ID and quantity. The native pending dispatcher also queues retained pre-5.4 shortages that have no command identity, under process-version concurrency. Restart does not regenerate committed identities or re-reserve stock. Migration recovery assumes those earlier shortage records contain a valid available quantity; damaged records are logged rather than guessed.

Sales subscribes to created and rejected stable logical topics before enabling its relay. It matches tenant, process/order/line, operation, original replenishment command causation, Stock Item, quantity and unit. Delivery fingerprints reject altered content under one Message ID; retained semantic fingerprints make equivalent outcomes under new delivery IDs no-ops and reject conflicting outcomes. Correlation mismatches are receipted/audited without changing the process. Transport producer headers are consistency checks, not authentication.

Sales commits the retained outcome, process version, curated activity, workflow audit and inbox in one transaction. Rejection requires attention; creation records the requirement identity and short number but leaves the line as a reservation shortage and the process awaiting replenishment. No stock change, automatic Purchase Order or fulfilment success follows from requirement creation. Human HTTP responses expose the retained quantity, requirement references and diagnostic rejection code, not a trusted workflow mutation capability.

The real Sales producer currently sends minimum reference revision zero: Inventory's reservation outcome has no item-reference revision. Purchasing still waits for missing/uninitialized references, but a known projection row is not proof of zero source lag. Adding a freshness guarantee requires an explicit contract change, not pretending the snapshot watermark is a per-item revision.

## Transactions and delivery

Resolve holds the existing Purchasing bootstrap checkpoint row lock through commit, serializing against bootstrap, reference updates, reconciliation and other resolves. This coarse module-wide lock favors a clear proof over throughput; it is not a general lock framework.

Requirement creation, completed request identity, inbox receipt, audit and created-event outbox commit in one explicit Purchasing transaction. A first SaveChanges obtains the PostgreSQL identity-generated short number; it does not commit. Later audit/outbox/inbox failure rolls everything back. Sequence gaps are allowed. Numbers are globally allocated within Purchasing and looked up only within an authorized Organization, not per-Organization counters.

The outgoing event uses a stable logical topic, Message ID and original request causation. The module-owned relay claims committed rows with a token/lease, publishes with confirms and marks dispatch afterward. A crash in that window can repeat the event; consumer idempotency remains required. The reference-delivery inbox stays separate from the workflow inbox.

All created requirements are open in this increment; there is no artificial one-member status enum or Purchase Order creation. Human get/list queries check current Organization context and `purchasing.requirements.view`, return bounded keyset pages and expose no workflow mutation.

## Limits and recovery

- Missing reference data has no automatic expiry yet. Pending intent stays queryable and retries slowly; operator deadlines/attention and monitoring belong to 5.6.
- Reconciliation restores missed/damaged references, after which the worker resolves against them. It does not recreate requirements or replay business audits.
- Source restoration, projection scale, two-replica races, payload retention and bounded broker shutdown remain later proof work. This increment extracts no messaging library.
- Created/rejected outcomes have no separate Sales response deadline yet; timeout/attention and retention policy closure remain in 5.6. A created requirement is not proof of subsequent replenishment or stock receipt.

## Evidence

`ReplenishmentRequirementTests` uses real PostgreSQL/RabbitMQ, Purchasing queries, Inventory setup Contracts and command/outcome messages. Cases cover duplicate intent/delivery, rollback after the first save, redrive, conflict/error routing, pending bootstrap, stale-reference recovery after restart, missing-row reconciliation, foreign tenant, base unit, inactive state and metadata/quantity rejection. Fault SQL only arranges failures or projection damage.

`SalesReplenishmentTests` exercises real Sales/Inventory/Purchasing composition, retained shortage restart, repeated delivery and post-commit/pre-ACK failure, semantic/delivery conflicts, foreign correlation, permanent rejection and Sales participant-failure rollback/redrive. Controlled Inventory outcomes isolate eligibility-rejection setup; they do not substitute for the successful real-peer round trip.

`LocalRuntimeTests.PurchasingRequirements` reuses authenticated Aspire topology for route registration, bounded query validation, current-role denial/revocation and mandatory URL scope. `LocalRuntimeTests.SalesOrderApproval` additionally follows the real shortage through Purchasing creation and Sales' retained result over authenticated HTTP, including a user without Purchasing permission. All proofs run in existing CI lanes.
