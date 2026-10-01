# Event-sourcing design and extraction direction

Status: Confirmed direction, with explicitly identified proposals and deferred work.

This note refines the [architecture baseline](architecture-and-delivery.md#8-event-sourcing-projections-and-audit). It records the intended design, not a claim that every part is implemented. Changes still follow reviewable increments and the owner's exact-change-set commit approval.

## Scope and vocabulary

- Stock Position is the first event-sourcing proof. A second concrete aggregate, preferably in another module, is scheduled before library extraction and final product scaffolding. Its domain and owning charter are chosen in that late increment; existing state-stored aggregates are not silently converted.
- Event-sourcing mechanics will become a separate, opt-in library at extraction. State-stored modules and consumers that do not use event sourcing must not depend on it, inherit its aggregate base types, or register its runtime. The library must not depend on business-module Contracts; owning modules retain domain decisions, projection definitions, authorization, audit choices, and business transaction orchestration.
- **Decision state** is the complete business data required by a decider. Name it `{Aggregate}State`, retaining `StockPositionState`: the same domain state can come from live reconstruction or an inline write model. Do not encode the loading lifecycle in its name. `Domain` is too vague, `DomainProjection` describes construction rather than the state, and `DomainAggregate` confuses passive state with the aggregate wrapper.
- **Decider** proposes events from decision state and a command. **Evolution / reducer** applies events to state. These are separate pure responsibilities, even if initially hosted beside one another.
- **Aggregate wrapper** is optional convenience for business operations, candidate-state validation, and pending events. It does not own EF, serialization, projection staging, or an original-state copy for persistence.
- **Projection** is an event-derived representation. **Projector** constructs or advances it. A write model supports decisions; a read model supports queries. They may share a shape but are not required to do so.
- **Live** means calculated on demand. **Inline** means persisted in the append transaction. **Async** means advanced separately after commit and allowed to lag. These are execution lifecycles, not different kinds of aggregate identity. C# `async`/`await` does not determine the lifecycle.
- **Snapshot**, in this repository, means a separate versioned hydration checkpoint used to skip historical events. An inline projection is continuously maintained query/write state. Marten also uses snapshot terminology for aggregate-shaped projections; that broader usage must not obscure our distinction. Separate checkpoint snapshots remain deferred.

## Decision handling and loading

- Inline aggregate-shaped write state is the recommended default, not mandatory for every aggregate. It must contain all decision-relevant data; totals alone are insufficient if commands require individual reservation identities.
- Load-for-writing obtains current decision state and captures/verifies the stream version. The concrete implementation uses its inline write model by default. Live reconstruction through the same write-state reducer remains available for historical reads, rebuild, and verification; generic lifecycle selection and session identity-map conveniences are not required now.
- A command proposes a complete event batch. Evolve the entire candidate batch and validate its final state before changing accepted state, version, or pending events. Intermediate state may violate business invariants; reducers do not rerun per-event command/input rules. Only event evolution changes event-sourced business state; do not mutate fields independently and append events describing the same change.
- Current eligibility/policies run during decisions, not replay. Reduction must be deterministic and have no clock, random-ID generation, external calls, audit duplication, or integration-message publication. Decode/upcast supported facts, then apply them without current business validation or normalization. Envelope/schema decoding, structural stream-order checks and arithmetic representability may reject unreadable history; they do not reevaluate whether an old command should have been accepted. Quantity restoration and recorded arithmetic are separate from validated new command construction.
- The business handler supplies actor, tenant, recorded time, and transaction scope explicitly. Persistence owns version checks, event envelopes, serialization, and database-concurrency classification.
- In-memory reduction is not the whole cost of live loading. PostgreSQL reads, transfer, JSON deserialization, allocations, and reduction all count. Measure realistic streams before claiming replay is negligible or adding checkpoint snapshots.

## Action availability and shared policies

- State, deciders, evolution, and domain policies remain internal to the owning module. Other modules and HTTP clients obtain capability-shaped availability results through Contracts, not domain objects or policy implementations.
- Distinguish general action availability from eligibility for a particular command. For example, reserving a specified quantity requires those command inputs; a generic reservation-available flag cannot guarantee acceptance.
- An application query combines current business eligibility with the caller's authorization and returns an advisory result with stable reason codes. The command handler rechecks authorization and eligibility against current state and protects the write through concurrency control. Availability is not a grant or a promise that a later command succeeds.
- When an actual availability query repeats decision rules, extract a pure action-specific evaluation that both the query and decider use. Do not independently maintain conflicting `CanX` and `DecideX` rules or introduce a generic policy framework before that need exists.
- Resulting-state invariant validation, such as `StockPositionPolicy.Validate(candidate)`, remains distinct from action eligibility. Replay applies recorded facts without rerunning current eligibility or authorization policies.

## Multiple projections and explicit operations

- Allow additional inline projections when an actual command/query needs them. Each projection owns its shape and reducer. It may explicitly ignore known irrelevant events; unknown event aliases/schema versions must not silently disappear.
- Reuse the write-state reducer when live and persisted models represent the same state. View-specific projections may reduce differently and need not mirror aggregate structure. Multi-stream summaries are a later real-use-case choice, not a generic framework requirement.
- Distinguish three operations: **project/preview** calculates a result in memory; **stage inline updates** advances registered persistence models for an accepted batch; **rebuild** reconstructs persisted projection data through the explicit administrative path.
- Proposed utility seam: a module-local, explicitly registered collection of projectors invoked by an explicit coordinator. It may calculate a chosen view with pending events without saving, or stage the relevant inline views for append. Do not discover workflows through `SaveChanges`/`ChangeTracker`, scan every loaded assembly, or generate application code. Exact interface names and generalized discovery remain an implementation decision after concrete consumers exist.
- Ordinary queries return committed data. Pending-event preview must be explicit and must not share mutable tracked state that gets applied twice during save. No generic Marten-like session machinery is required for the current slice.

## Identity, persistence, and consistency

- `EventStream` is a domain-neutral technical header: tenant, ID, stable stream/aggregate type, version, and timestamps. It must not have Stock Item or Stocking Location fields or a Stock Position-specific factory. Persisted type names never depend on CLR namespaces.
- **Accepted Stock Position lookup:** resolve item/location to stream ID through its required inline write model and enforce tenant-scoped uniqueness there, rather than maintaining another identity table. Projection loss/rebuild requires disabling affected writes until repair, and lifecycle retirement retains a lookup row. This operational restriction is not automatic detection of every missing row: the design does not independently defend against out-of-band deletion followed by creation using the same business key with expected version zero. A caller supplying a known nonzero version cannot reopen a missing model as a new stream.
- JSONB aggregate-shaped write documents with typed ID/tenant/version columns and targeted indexes are a storage proposal, not a requirement for every projection. PostgreSQL supports indexes on JSON expressions; equality/uniqueness needs an appropriate index/constraint, not a blanket GIN index. Missing identity fields must fail validation. Verify EF/Npgsql mapping and query translation before choosing it; flat views can remain relational.
- Events are immutable JSONB facts with stable IDs, stream ID/version, alias/schema version, recorded timestamp, global position, and minimized actor/tenant/correlation/causation/trace metadata. Never persist tokens or unnecessary personal data.
- EF optimistic concurrency protects stream version at commit in addition to any supplied expected-version check. Stream advancement, events, required inline projections, audit, and applicable outbox records share the handler-owned transaction. Multiple saves require an explicit transaction when they must be atomic.
- No HTTP between modules and no generic multi-stream/distributed transaction abstraction. Aggregate coordination is explicit in a same-module use case or a durable separate-commit process.

## Event evolution, audit, and messaging

- One attribute-declared event alias/schema-version registry drives both serialization and deserialization; missing/duplicate registrations fail composition. Related payloads may share one events file; incompatible CLR payload versions may use `V1`/`V2` suffixes.
- Additive fields retain a version only when defaults preserve old meaning and fixtures prove compatibility. Incompatible schemas get explicit readers and **upcasters**: pure read-time transformations into the current semantic representation. Stored history is not rewritten.
- New decisions emit the current event representation. Evolution/projectors can consume normalized current events rather than every payload version. Start with a concrete old-to-current conversion when a real version change occurs; compose adjacent transformations when that avoids repeated conversion logic. No generic graph of upcasters now. Upcasting must not invent historical facts or use current external data to reinterpret history.
- State-stored aggregate domain-event objects are transient unless explicitly persisted/mapped. Newly accepted event-sourced events enter the durable stream; historical events never enter the pending collection or get redispatched. Shared base classes are not required until useful duplication exists.
- Audit remains separate: accepted changes and security-significant denied/operational outcomes are not all represented by event-sourced facts. Write appropriate audit explicitly in the business transaction for now.
- Integration events are separate stable Contracts types. Application code explicitly publishes the chosen integration message; no automatic domain-event mapping pipeline is planned. Proposed future publisher seam stages the message in the owning module's transactional outbox. Actual broker delivery occurs after commit through the dispatcher; a method named `Publish` must not secretly perform a non-atomic external send.
- New consumers bootstrap from owner-produced current-state export plus integration tail and reconciliation, never by reading another module's private event history.

## History, rebuild, and deferred async work

- Corrections append events; do not edit old events. Retirement is a lifecycle fact, not normal history deletion. Privacy erasure is a separate governed operation.
- Version/as-of reads reconstruct currently committed recorded facts, with inclusive UTC cutoffs and stream-version ordering for equal timestamps. Global sequence numbers do not define stream order: EF may reorder event inserts inside one atomic batch. They also do not define commit order. Effective-time/bitemporal semantics and historical SQL transaction snapshots are deferred.
- Replay/rebuild only derives state: no email, messages, new business decisions, or repeated historical audit. Rebuild defaults to full reconstruction and atomic replacement under writer coordination; resumable shadow progress is deferred. Required lookup/write models cannot be emptied while affected commands remain enabled.
- **Async projections are explicitly wanted later.** Begin with one native background worker and a concrete eventual-consistency use case; no leader election or HA daemon framework. The consumer owns initial deployment scale.
- Competing workers are viable only with durable claiming/checkpoints, atomic progress plus projection effects, replay/idempotency, and ordering/serialization for each affected projected key. Avoid checkpointing past unprocessed work. A PostgreSQL sequence's maximum is not automatically a safe committed-event cursor because concurrent transactions can commit out of sequence. Design those mechanics with the first async use case; extra replicas are unsupported until the relevant proofs pass.

## Temporal reads and curated history contract

- The current-state HTTP route remains `/api/o/{organizationSlug}/inventory/stock-positions/{locationCode}/{sku}`. Optional `version` or `recordedAt` selects historical state; specifying both is invalid. Contract methods separate current, exact-version, and recorded-time queries. Each uses current authorization, not historical grants.
- Versions are positive event ordinals. A version beyond the captured stream head or a cutoff before opening returns not found. An exact version can select an intermediate event inside a multi-event append batch; it is not a historical transaction snapshot. Replay never accepts pending events, saves changes, audits the old action again, or publishes effects.
- Recorded timestamps come from the application clock at append, before database commit. An as-of query reads currently committed events recorded at or before its cutoff, normalizes supplied offsets to UTC, and includes all events sharing the boundary timestamp. Database timestamps have microsecond resolution. The implementation does not claim commit-time visibility or bitemporal correctness.
- All events in one append share the exact recorded timestamp. The handler commits the complete batch, stream header and inline models atomically: production delays never make half an append visible. Equal timestamps prevent time cutoffs inside a batch but are not batch identity. Exact event-version reads may deliberately expose an intermediate state that violates decision-boundary invariants; they neither validate it nor claim it is an accepted command-boundary state.
- New appends must not move recorded time backwards within a stream; clock regression fails before saving rather than inventing a new timestamp. Platform clock synchronization remains an operational requirement. Replay rejects timestamp regression and noncontiguous event versions in its selected prefix.
- `/history` returns ascending, business-facing opening/receipt/correction entries with version, recorded time, and applicable quantity in the position's base unit. Receipt quantity is an increase; correction quantity is the new absolute on-hand amount and includes its business reason. It exposes no raw JSON, event aliases/schema versions, global sequence, or unrestricted metadata. It is not the security audit log.
- History uses an exclusive `afterVersion` cursor and a `limit` default of 50, capped at 100. Empty/end/beyond-head pages have no next cursor. Each page captures its own committed head; pagination is not a stable snapshot export for integration bootstrap.
- Stock Position integrity faults carry stream identity, a failure classification, and applicable expected/observed versions. Unknown event identities/versions, malformed required payloads, gaps, and inconsistent required models remain exceptions, not normal business rejection results. Production HTTP uses the existing generic 500 Problem Details with trace ID; operators investigate/repair, and clients receive no internal repair instructions.
- Literal v1 JSON fixtures cover opening and receipt payloads independently of current CLR names. New versions need retained compatibility fixtures; do not regenerate old fixtures from the current serializer as a shortcut. No artificial v2 schema or generic upcaster is introduced without a real change.

## Stock Quantity Correction contract

- `POST /api/o/{organizationSlug}/inventory/stock-positions/{locationCode}/{sku}/corrections` accepts `onHandQuantity`, `reason` and `expectedVersion`, under the existing stock-adjust permission and BFF antiforgery guard. The quantity is an observed absolute amount, not a signed delta or a retroactive edit to a receipt.
- Correct only an already opened position. Missing reference/position with expected version zero returns not found; a known nonzero version with a missing write model remains a version conflict requiring investigation. Inactive reference data does not prevent reconciliation of an existing position; it still prevents new receipts.
- Zero is allowed; negative, over-precision, out-of-range and below-reserved quantities are rejected. The aggregate evolves candidate state and checks invariants before accepting the correction. The decider emits a new `inventory.stock-position.quantity-corrected` v1 event; normal append handles version/concurrency and atomically commits stream, inline model and accepted-change audit.
- Reasons are trimmed, require 1–200 characters and reject control characters. They are durable business text visible to authorized timeline readers; operators must not enter secrets or unnecessary personal data. Audit references the correction's quantity/version without duplicating the free-text reason.
- These reason rules apply to new corrections only. Replay ignores the explanatory text for decision-state evolution; history preserves the recorded reason without current-rule validation or re-normalization. `StockPositionHistoryEntry.Reason` is optional business explanation contextualized by its action, not a machine eligibility code or unrestricted metadata container. Keep future timeline fields grounded in actual actions.
- A valid correction equal to current on-hand quantity returns unchanged without another event or success audit. Expected version is still checked first; a stale request does not become successful merely because its target quantity happens to match. This is not a generic business-operation deduplication protocol.
- Replay changes only derived state. It does not rerun current command eligibility, publish messages, or write another audit. The retained literal correction fixture proves hydration alongside the earlier v1 opening and receipt facts. Administrative rebuild remains the separate 3.4b delivery part.

## Administrative Stock Position rebuild

- `IStockPositionProjectionRebuilder.RebuildAsync` is an Inventory-owned administrative contract, not an HTTP endpoint. It requires verified actor/Organization context and current `inventory.projections.rebuild` permission, granted by the code-defined Inventory Manager role. An Organization Administrator is not implicitly authorized. Operators/in-process tools establish context; no administrative console or CLI is shipped.
- Start with the known Stock Position ID, including when its lookup model is missing. Acquire the exclusive Organization writer gate, then capture the committed stream head and reconstruct from event version one. Replace/insert only this serving row and add one operational rebuild audit in the same transaction. Historical events/business audits are never repeated or modified, and replay does not publish integration messages.
- Failure/cancellation before commit leaves the serving model untouched. Retry in a fresh DI scope reconstructs from the beginning; there is no durable job, shadow table, checkpoint, resume lifecycle or retry scheduler. A lost response after commit can mean success already occurred. Another invocation is a new administrative operation and can add another rebuild audit; no exactly-once request guarantee is claimed.
- `PreviousModelMatched` compares the pre-repair row with reconstructed identity, quantities, version and final event timestamp. It is a diagnostic, not an independent semantic oracle: both hydration and inline evolution use the same reducer. Fixed expected-state examples and retained compatibility fixtures must establish historical meaning.
- Receipt/correction handlers acquire a shared PostgreSQL transaction-level advisory gate **before loading decision state**, retaining it through commit. Reconstruction takes the exclusive form before reading history. Earlier writers finish first; later writers load only after repair. Normal writers remain concurrent with EF optimistic concurrency. The Store rejects writes outside its gated transaction. Locks release on transaction completion/rollback/disposal; this application protocol does not enforce arbitrary SQL.
- Unknown event schemas, gaps, invalid ordering/payloads and inconsistent required models remain structured Inventory integrity exceptions. Rebuild is not a command and does not rerun current domain input/eligibility rules.
- For actual projection loss or known corruption, stop affected writes operationally first. Business-key identity still depends on the required write model: out-of-band deletion followed by expected-version-zero creation can open another stream. Keep known stream IDs from diagnostics/history; successful repair must precede resuming affected writes.
- Full reconstruction consumes stream-sized memory and blocks **all participating Stock Position writers in that Organization**, not readers or other Organizations. This is a small-stream repair proof, not a zero-downtime daemon. Measure cost before scaling. Resumable shadow reconstruction is deferred until measured recovery requirements justify it; persisted progress would need reducer/projection revision compatibility.

The protocol follows [PostgreSQL transaction-level advisory locking](https://www.postgresql.org/docs/18/explicit-locking.html#ADVISORY-LOCKS) and [EF Core explicit transaction ownership](https://learn.microsoft.com/en-us/ef/core/saving/transactions). No new dependency or schema is introduced for rebuild.

## Post-3.4 seam review

Technical load/append, codec, ordered event reading, inline projection and administrative repair remain concrete in Inventory. Future extraction candidates are range/recorded-time integrity checks, hydration and writer coordination. Inventory identity, policy, permissions, audit choices and projection shape stay module-owned. The second aggregate in 8.1 validates these seams; 8.1b resolves or explicitly constrains core correctness before extraction in 8.2. Durable shadow jobs and an async projection framework are not prerequisites.

## Core-correctness follow-up register

Increment **8.1b — Event-sourcing correctness and extraction gate** must revisit these limits; earlier real workflows may bring individual fixes forward:

- Projection-dependent business-key identity: prevent accidental reopening after lookup loss, or clearly bound library support and operational repair.
- Coarse Organization gate and full-stream replay cost: measure realistic streams, narrow coordination only with tested discovery/creation and lock ordering.
- Shared reducer correctness: retain independent expected semantic fixtures; successful replay alone is not proof of business correctness.
- Atomic event/projection/audit/inbox/outbox changes: prove the combinations when durable workflows introduce real consumers.
- Resumable/online rebuild remains conditional on recovery cost. If adopted, require versioned checkpoint semantics, atomic progress and serving-state separation; otherwise keep full reconstruction and document its limits.

## Late validation and extraction

After the reference deployment works, implement a second concrete event-sourced aggregate in Increment 8.1, preferably under a different owning module. It should exercise a distinct decision-state shape and more than one justified inline view, not merely duplicate Stock Position with renamed quantities.

Increment 8.2 extracts only demonstrated reusable stream, codec/upcasting, hydration, projection-coordination, and concurrency mechanics. Domain policy, projection definitions, and module-owned transactions remain local. The reference behavior becomes the sample; do not import Marten/Wolverine code generation or build a general application framework.

The final handoff increment rehearses minimal configuration-driven product scaffolding against a real second repository and deploys it. A bounded naming/configuration script is sufficient; provider/persistence options are included only when their implementations have actually been exercised. A broad wizard or plugin generator remains uncommitted scope until that increment chooses a concrete need.

## Primary references

- [Marten projection roles and lifecycles](https://martendb.io/events/projections/)
- [Marten fetch-for-writing](https://martendb.io/scenarios/command_handler_workflow.html) and [pending-event projection](https://martendb.io/events/projections/project-latest)
- [Marten event upcasting](https://martendb.io/events/versioning#upcasting-advanced-payload-transformations)
- [Npgsql JSON mapping](https://www.npgsql.org/efcore/mapping/json.html) and [PostgreSQL JSONB indexing](https://www.postgresql.org/docs/current/datatype-json.html#JSON-INDEXING)
- [PostgreSQL timestamp precision](https://www.postgresql.org/docs/current/datatype-datetime.html) and [Npgsql UTC date/time mapping](https://www.npgsql.org/doc/types/datetime.html)

These references inform the design; no Marten dependency is introduced.
