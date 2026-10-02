# Stock Item reference bootstrap

This is a concrete Inventory/Purchasing protocol, not a generic bootstrap library or a promise of service extraction without further work. [Primary-source research](../research/stock-item-snapshot-tail.md) explains the PostgreSQL and RabbitMQ guarantees used here.

## Producer boundary

Inventory publishes `StockItemReferenceChangedV1` as complete reference state: Organization, Stock Item identity, SKU, description, immutable base unit, active status and source revision. Its stable logical topic is `inventory.stock-item-reference-changed.v1`; CLR names do not determine routing. Creation, description changes and activation changes participate. No-op operations publish nothing.

Every participating handler owns an explicit Inventory transaction and locks the singleton `stock_item_reference_feed` row **before** loading/changing an item. It stages the next revision, item state, business audit and outbox row together, then commits. The lock is held until commit or rollback: revision order is therefore committed-prefix order, unlike PostgreSQL sequence allocation. It serializes Stock Item metadata writes across Organizations, not Stock Position receipts/reservations/appends. This small-catalog throughput trade-off is deliberate and must be measured before widening its use.

The item's `reference_revision` is technical EF shadow metadata, not an aggregate business rule. Locked rows are refreshed explicitly because EF tracking queries do not replace values already tracked in a reused scope. Refreshing only the relevant rows preserves other context state; no ChangeTracker clearing, SaveChanges override or hidden event dispatch is added. Handler contexts must not contain unsaved edits to the same item before an administration command.

Existing rows introduced before this feed have revision `0`, and the migrated feed starts at `0`. They are supplied by the initial full snapshot; their next real mutation receives a positive revision. Do not reset/reseed a live feed or treat the cursor as a business identifier.

`IStockItemSnapshotExporter` is a trusted, non-HTTP full-catalog capability. One read-only Repeatable Read transaction reads the counter first, then the complete catalog, including inactive items. Both queries see the same database snapshot. The export deliberately bypasses only Inventory's named Organization filter and includes explicit Organization IDs. It materializes at most 10,000 items; exceeding the bound fails initialization rather than silently truncating data. Pagination across unrelated transactions is not supported.

## Consumer boundary

Purchasing owns `modulith-foundry.purchasing`, its isolated Rebus provider and `modulith-foundry.purchasing.error`. The host composes Purchasing before enabling the Inventory producer. Subscription completion signals a process-local barrier; both the native bootstrap worker and direct `IStockItemProjectionBootstrapper` callers wait for that barrier before exporting. A fresh database is not initialized just because a worker was registered.

Purchasing persists its checkpoint, inbox receipts and reference rows in its own schema. Full-state rows double as a **durable coalesced buffer** while initialization is incomplete; ordinary projection queries return no item until the checkpoint is Ready. The snapshot import merges each row only when its revision advances that item. This preserves a newer event already received during export without retaining an entire event history. Snapshot installation, its watermark and Ready commit in one local Purchasing transaction, serialized with handlers by the checkpoint row lock.

After initialization:

- Events at/below the snapshot watermark are already represented and require only delivery deduplication.
- Events above it advance only the addressed `(Organization, Stock Item)` row when newer. Reordered events cannot regress a row, and a large revision for one item cannot discard a smaller pending revision for another.
- Inbox receipt and projection effect commit before successful handler return/ACK. Repeated MessageId/content has no new effect; conflicting MessageId/content is poison. For states eligible for application, a repeated current per-item revision with different state is also poison. States covered by the snapshot boundary or superseded by a newer item revision are ignored rather than retained as a semantic-event ledger.
- Fingerprints explicitly select the V1 fields and normalize the timestamp; they are not reflection-based hashes of an evolving arbitrary DTO.
- The persisted watermark remains the **snapshot boundary**, not “last event seen”, a contiguous delivered-tail checkpoint, or proof that all messages have arrived.

The projection contracts are trusted workflow/administrative capabilities, not human authorization adapters. Reads require an explicit Organization ID, and no HTTP endpoint exposes them. Normal EF reads fail closed without an Organization context; bootstrap and validated message handlers use explicit own-schema administrative queries. No module reads another module's tables. Purchasing.Contracts does not gain an Inventory.Contracts DTO dependency; its projection view owns its representation and carries opaque identities.

## Restart and failure semantics

The bootstrap worker retries in fresh scopes. Before Ready, retry captures a fresh snapshot and preserves newer buffered rows; there is no durable snapshot download or resumable paging session. Snapshot installation is atomic. After Ready, process restart rebinds the same durable queue and retains the checkpoint/projection instead of downloading a replacement snapshot. At-least-once delivery remains intentional; publisher confirms alone never prove a subscription existed.

The counter and consumer checkpoint are different locks in different transactions. Inventory export finishes before the Purchasing installation transaction starts. There is no shared cross-module database transaction, HTTP between modules, generic mediator or private Inventory-event replay.

## Evidence and remaining scope

`StockItemBootstrapTests` use Inventory administration/export and Purchasing bootstrap/query Contracts against PostgreSQL, plus the real production RabbitMQ endpoints. They cover pre-existing inactive data, literal committed revisions, a writer committing between snapshot queries, writes during bootstrap, duplicate/new-identity/reordered full states, tenant isolation, audit-failure rollback, retained-checkpoint restart, and reused-context interleaving. The late-consumer case drains historical publications through the real producer before Purchasing subscribes: pending outbox replay cannot substitute for snapshot installation. SQL observes dispatch completion only to coordinate that setup. A test-only EF command-log barrier pauses actual snapshot execution; it is race coordination, not a replacement exporter or an asserted product interface. The existing broker CI lane runs them.

`StockItemProjectionRecoveryTests` extend the same broker lane with forced process termination during snapshot export and installation, before receiver commit and after commit/before ACK; retained-queue worker cutover; repair rollback; updates arriving during repair; payload/identity poison; storage-failure redrive; missing/damaged/unexpected projection rows; and repair of an actual error-queued Inventory publication followed by harmless late redrive. Test SQL and command-log/pipeline checkpoints coordinate failures only. `BrokerReceiver` is a test-only child executable using production composition. Ordinary restart is not substituted for abrupt termination.

## Explicit comparison and repair

`IStockItemProjectionReconciliation` is a trusted administrative Contract, not an HTTP endpoint or automatically scheduled repair loop. `InspectAsync` reports missing, differing and unexpected snapshot-covered rows without modifying serving state. Differences may represent ordinary delivery lag, not corruption. `RepairAsync` returns that same **pre-repair** comparison and commits authoritative snapshot-covered state plus the new snapshot boundary atomically. Successful repair emits a structured operational warning with counts/boundary, not a business audit replay, outgoing message or raw payload. Capture this operational log in the configured telemetry backend; no new durable repair-job/audit framework is introduced.

Both operations wait for subscription binding, export a fresh Inventory snapshot, then lock Purchasing's checkpoint to serialize comparison/repair with receivers. Ready is required. Rows with a revision above the exported watermark are preserved and reported separately; they may have arrived while export ran. Snapshot-covered rows can be replaced even at an equal revision (including damaged SKU/base unit), because repair is deliberately distinct from normal message evolution. Unexpected rows at/below the watermark are removed; no Inventory row is modified. Inbox identities are retained. A successful repair advances the boundary only because the complete snapshot has been installed—not because a high-numbered message arrived.

`IsConsistent` means no differences were observed **among snapshot-covered rows**. Newer rows are not verified against an older snapshot; take another fresh comparison once producers/tail settle. It is not proof of zero lag or queue health. A snapshot older than the installed boundary fails without mutation. Concurrent repair may make an earlier snapshot obsolete: retry fresh. Persistent regression requires coordinated restore and is not automatically repaired. Damage that fabricates a revision ahead of the source is outside automatic repair; quiesce and investigate rather than force a downgrade.

Comparison/repair materialize the small catalog and hold the checkpoint lock while comparing/saving. Existing tracked rows are refreshed under that lock so reused administrative scopes do not overwrite newer consumer state; callers must not have unrelated pending projection edits. This bounded operation can block reference receivers. It is not a resumable or online large-catalog rebuild.

## Recovery runbook and endpoint cutover

1. For error-queued messages, diagnose schema/content/identity versus transient storage failure first. Fix the cause; never repeatedly redrive a conflicting identity or edit its payload under the same MessageId.
2. Explicit redrive sends the unchanged original contract/content and MessageId to `modulith-foundry.purchasing`, preserving the stable type alias and `producer-module=inventory`. Transport retry/error headers require the chosen operator tool's documented handling. The test error observer consumes a failed message and deliberately resends it with those original application fields; it is **not** a shipped raw-error-queue administration tool.
3. For suspected lag/drift, invoke the registered comparison Contract in a fresh scope. If needed, invoke explicit repair, then compare again against a fresh snapshot. Older late deliveries are now snapshot-covered and harmless. Keep the original inbox and inspect error queues; repair does not prove broker health.
4. For a lost queue, first restore the consumer's normal queue/binding with its production composition, then explicitly repair against a fresh snapshot and verify. Ready alone does not trigger rebootstrap or detect queue loss. This path relies on Inventory retaining the complete reference catalog. Actual queue deletion is not exercised by this increment; missed-publication repair is exercised through a real error-queued update.
5. For endpoint cutover, stop the monolith-owned Purchasing endpoint completely before a worker takes the **same queue**, schema and retained checkpoint/inbox. The test first queues an update while no receiver runs, then starts the worker and applies it plus a later live update without reinstalling the snapshot. Do not activate two independently configured owners as a cutover strategy.

The test child still composes Inventory's implementation solely to supply the in-process snapshot Contract. It proves moving endpoint execution, **not** removing Inventory from the worker or deploying a service. A genuinely extracted Purchasing service needs a reviewed remote snapshot adapter, authorization/configuration and bootstrap lifecycle. No production worker artifact, HTTP between modules or deployment change is introduced here.

Limits to keep visible:

- Complete-state coalescing works for the current no-physical-deletion Stock Item lifecycle. A future deletion needs a tombstone and snapshot reconciliation semantics; a delta event cannot use this algorithm unchanged.
- Both serialization points and the materialized catalog have bounded sample scale. No async projection daemon, leader election, generic bootstrap framework or library extraction is introduced.
- There is no historical integration-event replay API, automatic queue-loss detection, retention worker, or exactly-once external delivery. Error-queued events leave the view stale until repaired/redriven; Ready means initialized, not zero lag or confirmed healthy tail.
- Restoring Inventory independently of Purchasing/broker state can regress a cursor and leave future consumer state/messages behind. Coordinated restore/reconciliation or a reviewed feed-generation protocol is required before the deployment recovery claim in 7.3; restarting alone is not repair.
- Extracting Purchasing still needs a real transport adapter for snapshot export and bootstrap lifecycle coordination. Extracting Inventory also requires redesign of the synchronous Sales reference/location queries and the already documented Access authorization-guard assumptions. A stable queue is useful support, not seamless extraction.

Replenishment Requirements remain Increment 5.4; compensation remains 5.5. Bootstrap mechanics can be compared during later reliability extraction, but this increment creates no shared bootstrap library.
