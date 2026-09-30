# Event-sourcing design and extraction direction

Status: Confirmed direction, with explicitly identified proposals and deferred work.

This note refines the [architecture baseline](architecture-and-delivery.md#8-event-sourcing-projections-and-audit). It records the intended design, not a claim that every part is implemented. Changes still follow reviewable increments and the owner's exact-change-set commit approval.

## Scope and vocabulary

- Stock Position is the first event-sourcing proof. A second concrete aggregate, preferably in another module, is scheduled before library extraction and final product scaffolding. Its domain and owning charter are chosen in that late increment; existing state-stored aggregates are not silently converted.
- **Decision state** is the complete business data required by a decider. Name it `{Aggregate}State`, retaining `StockPositionState`: the same domain state can come from live reconstruction or an inline write model. Do not encode the loading lifecycle in its name. `Domain` is too vague, `DomainProjection` describes construction rather than the state, and `DomainAggregate` confuses passive state with the aggregate wrapper.
- **Decider** proposes events from decision state and a command. **Evolution / reducer** applies events to state. These are separate pure responsibilities, even if initially hosted beside one another.
- **Aggregate wrapper** is optional convenience for business operations, candidate-state validation, and pending events. It does not own EF, serialization, projection staging, or an original-state copy for persistence.
- **Projection** is an event-derived representation. **Projector** constructs or advances it. A write model supports decisions; a read model supports queries. They may share a shape but are not required to do so.
- **Live** means calculated on demand. **Inline** means persisted in the append transaction. **Async** means advanced separately after commit and allowed to lag. These are execution lifecycles, not different kinds of aggregate identity. C# `async`/`await` does not determine the lifecycle.
- **Snapshot**, in this repository, means a separate versioned hydration checkpoint used to skip historical events. An inline projection is continuously maintained query/write state. Marten also uses snapshot terminology for aggregate-shaped projections; that broader usage must not obscure our distinction. Separate checkpoint snapshots remain deferred.

## Decision handling and loading

- Inline aggregate-shaped write state is the recommended default, not mandatory for every aggregate. It must contain all decision-relevant data; totals alone are insufficient if commands require individual reservation identities.
- Load-for-writing obtains current decision state and captures/verifies the stream version. The concrete implementation uses its inline write model by default. Live reconstruction through the same write-state reducer remains available for historical reads, rebuild, and verification; generic lifecycle selection and session identity-map conveniences are not required now.
- A command proposes a complete event batch. Evolve candidate state and validate it before changing accepted state, version, or pending events. Only event evolution changes event-sourced business state; do not mutate fields independently and append events describing the same change.
- Current eligibility/policies run during decisions, not replay. Reduction must be deterministic and have no clock, random-ID generation, external calls, audit duplication, or integration-message publication. Representation checks may reject corrupt historical data.
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
- Proposed Stock Position simplification: resolve item/location to stream ID through its required inline write model and enforce tenant-scoped uniqueness there, rather than maintaining another identity table. The lookup dependency is not yet final: it requires accepting that projection loss/rebuild disables affected writes until repair, and lifecycle retirement retains a lookup row. It does not independently defend against out-of-band deletion of unknown rows followed by creation using the same business key.
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
- Version/as-of reads reconstruct what was recorded, with explicit UTC inclusivity and sequence ordering for equal timestamps. Effective-time/bitemporal semantics are deferred.
- Replay/rebuild only derives state: no email, messages, new business decisions, or duplicate audit. Rebuild uses shadow state, verification, cancellation/resume, and a writer-coordinated swap. Required lookup/write models cannot be emptied while affected commands remain enabled.
- **Async projections are explicitly wanted later.** Begin with one native background worker and a concrete eventual-consistency use case; no leader election or HA daemon framework. The consumer owns initial deployment scale.
- Competing workers are viable only with durable claiming/checkpoints, atomic progress plus projection effects, replay/idempotency, and ordering/serialization for each affected projected key. Avoid checkpointing past unprocessed work. A PostgreSQL sequence's maximum is not automatically a safe committed-event cursor because concurrent transactions can commit out of sequence. Design those mechanics with the first async use case; extra replicas are unsupported until the relevant proofs pass.

## Late validation and extraction

After the reference deployment works, implement a second concrete event-sourced aggregate in Increment 8.1, preferably under a different owning module. It should exercise a distinct decision-state shape and more than one justified inline view, not merely duplicate Stock Position with renamed quantities.

Increment 8.2 extracts only demonstrated reusable stream, codec/upcasting, hydration, projection-coordination, and concurrency mechanics. Domain policy, projection definitions, and module-owned transactions remain local. The reference behavior becomes the sample; do not import Marten/Wolverine code generation or build a general application framework.

The final handoff increment rehearses minimal configuration-driven product scaffolding against a real second repository and deploys it. A bounded naming/configuration script is sufficient; provider/persistence options are included only when their implementations have actually been exercised. A broad wizard or plugin generator remains uncommitted scope until that increment chooses a concrete need.

## Primary references

- [Marten projection roles and lifecycles](https://martendb.io/events/projections/)
- [Marten fetch-for-writing](https://martendb.io/scenarios/command_handler_workflow.html) and [pending-event projection](https://martendb.io/events/projections/project-latest)
- [Marten event upcasting](https://martendb.io/events/versioning#upcasting-advanced-payload-transformations)
- [Npgsql JSON mapping](https://www.npgsql.org/efcore/mapping/json.html) and [PostgreSQL JSONB indexing](https://www.postgresql.org/docs/current/datatype-json.html#JSON-INDEXING)

These references inform the design; no Marten dependency is introduced.
