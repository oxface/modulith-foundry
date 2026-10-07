# Event-sourcing directions and deferred capabilities

Status: current supported scope and deferred directions, 2026-10-07. This is the library-local
capability record; repository plans/reports supply review history and execution evidence.
[The concrete store](../../../docs/plans/es1-library-write-store.md) and [new proofs](../../../docs/reports/es1-library-write-store.md)
now implement tracked main-state participation. This catalog does not authorize deferred
features. Marten remains a reference, not a dependency.

## Current package capabilities

This optional EF Relational adapter references the package-free EventSourcing aggregate core.
It provides native stream/envelope mapping, a default StoredEventRecord, prepared ordered
append, a provided aggregate write store and explicit required inline projection/save validation.
Handlers use IEventStore<TAggregate>; aggregate family, encoding, rows and projection bindings
are configured once. Native providers, DbContext, migrations, domain facts/reducers, admission
and final SaveChanges/commit remain consumer-owned. No DI container or sample module is required.
See [the setup and error contract](../README.md).

StoredEventRecord is the optional non-tenant envelope; Payload is heterogeneous JSON and
EventName/SchemaVersion select its exact CLR decoding contract. Custom IStoredEventRecord
implementations support owned composite keys/extra fields. One envelope can hold many fact
types. The aggregate-state projection and required secondary projections are persistence views
of the same stream, not additional command aggregates or child entities of the command root.

The store uses InlineProjectionStorage<TStream,TRow> to load each required row by its complete
mapped foreign key, validate its captured version/time and stage fresh detached candidates.
It binds loaded streams and roots to the caller's transaction. Aggregate-state encoding uses
the already evolved candidate; secondary reducers evolve their own committed state. The native
save guard requires all registered projections and event positions to advance together.

Named DbSet properties on a consumer context are optional conveniences. Set<T>() queries or
adds configured entities; Entry(instance) accesses tracking state. Metadata registration uses
the native model, not a context marker or named property. Storage mapping is once per table
pair; aggregate-state and additional required projections register per stream family. Install
the save validator in both native save overrides; model declarations alone do not execute it.
The store refuses write loading/appending without an explicit transaction.

## Main inline state and native reads

For aggregate writes, the owner requires events and the main inline aggregate state to
commit together. The persisted state carries the stream version it represents. An append
must advance events, stream header and main state to the same version in the consumer's
explicit native EF transaction; a failure must leave all three unchanged. Additional
required synchronous views participate in that transaction. Other projections can be
asynchronous when their eventual-consistency contract is separately implemented and proven.

Ordinary commands load version-checked inline state. Native EF queries read that persisted
state without reconstructing event history. Explicit historical reconstruction remains a
separate entry point, using the owning module's evolution and producing no pending facts.
Snapshot-plus-tail loading is a future capability, not a fallback for broken required inline
state. Missing or inconsistent required state still fails under the existing consumer policy.

The owner resolved the scope: main state is mandatory for registered aggregate writes;
raw streams remain permitted. [ADR 0006](../../../docs/adr/0006-transactional-main-inline-state.md) records
that boundary. The provided store stages configured participants; explicit native model/save
integration checks that main and required secondary state accompany the contiguous event batch
and advancing header. Calling the lower-level appender alone cannot bypass those configured
save checks. Native saves still belong to the consumer. This does not cover arbitrary SQL,
bulk/external writes, bypassed overrides or semantic state-body tampering.

The counter remains a valid raw-stream adopter using captured history/direct JSON without
inline state. Aggregate bookkeeping alone does not register the stronger guarantee. Native
availability-filter translation and required-save omission/recovery now have PostgreSQL proofs
in the follow-up report; prior E6/appender results remain separate evidence.

Use module-local `{Aggregate}Queries` for related read operations or
`{Aggregate}{Specific}Query` for an individual query. These compose native EF selection,
ordering, paging and result projection. `{Aggregate}Filters` can provide reusable
`IQueryable<T>` extensions or expression predicates over mapped state. Materialize results
inside the owning module; business Contracts do not expose its EF query provider.

A calculated CLR property or domain method is not automatically SQL-translatable. Express
filters in terms of mapped fields using supported expressions; avoid `Compile()` or switching
to enumerable evaluation before filtering/paging. Pass an explicit observation time for
time-dependent status. When a formula is shared with domain behavior, verify both the semantic
result and its PostgreSQL translation; an in-memory query test does not establish database
behavior. Persisted columns or typed JSON state are possible mappings, to be selected with
actual query usage. These names are consumer/template conventions, not a generic read store
or an event preset added to T1.

## Durable event registrations

The archive already used registries for event-sourced facts, not just transient domain-event
dispatch. [Purchasing's serializer](../../../archive/proof-sample/modules/Purchasing/Purchasing/PurchaseOrders/Persistence/PurchaseOrderEventSerializer.cs)
and [Inventory's serializer](../../../archive/proof-sample/modules/Inventory/Inventory/StockPositions/Persistence/StockPositionEventSerializer.cs)
scan their family assembly for concrete event types and require a `StoredEventType` attribute.
They map runtime CLR type to durable name/schema version when writing, and the exact durable
name/version pair back to a CLR type when reading. Invalid or duplicate identities fail;
unknown identities and invalid payloads become module integrity errors. This is historical
source evidence, not a new test run or a mandate to preserve assembly discovery.

The active [JSON codec](../../ModulithFoundry.Events.Serialization/README.md) already
extracts those registry mechanics with explicit `EventRegistration<TEvent>` entries. It
rejects conflicting registrations and unknown read identities; it does not dispatch handlers,
upgrade payloads or infer identities from CLR names. Reuse that mechanism where selected
rather than introduce a second event catalog. Keep families independently adoptable.

If a future mixed-family store needs one decoding scope, compose its explicitly selected
registrations and check durable-identity collisions within that scope. A shared timeline does
not require a universal domain-event base or handler bus. Current codecs support one write
identity per concrete type and exact-pair reads; historical read schemas and upgrade chains
need a separately reviewed extension. The independent counter's direct JSON encoding remains
a supported choice under ES1.

## JSONB payloads versus queryable state

`JsonElement` is a supported Npgsql EF mapping for JSON columns. DOM mapping suits envelopes
whose payload shape varies by durable event identity; stable typed JSON models have richer
query support through EF-aware mapping. On EF 10, Npgsql recommends complex types with
`ToJson()` for typed JSON models. See [Npgsql JSON mapping](https://www.npgsql.org/efcore/mapping/json.html).

For immutable event envelopes, retain the current JSONB payload and decode it through the
selected registry into typed domain facts. A single mapped CLR payload shape would not cover
all event schemas. Keep DOM access at the persistence/codec boundary, and ensure a payload
does not outlive its source document; ES1 clones prepared payloads. Switching to strings
would change the representation without resolving durable schema selection or typed decoding.

Queryable main state has different needs: ordinary columns or a typed EF-aware JSON model
can make reusable filters easier to translate and index. The current DOM mapping does not
prevent supported JSON queries, but it does not grant arbitrary aggregate methods SQL
translation. The provided store hides stored-row generics and DOM from write handlers. Its fresh-context
JSONB test preserves nested data, Unicode and decimal precision after disposing source
documents. Native availability queries use mapped inline state rather than event payloads.
No state schema, fixture or migration replacement is introduced.

## Provider support and future provider-specific adapters

The package references EF Relational, not Npgsql. PostgreSQL 18.6 with EF 10.0.12 and Npgsql
EF 10.0.3 is the only verified database configuration. JSONB mapping and the known PostgreSQL
constraint classification remain explicit consumer setup. Registering a different provider
is not currently rejected by a deliberate provider check and does not establish support.
SQL Server may be an adopter candidate; SQLite may have different JSON, transaction and
concurrency limitations. Neither is certified by the current proofs.

Native version predicates provide optimistic concurrency now; an explicit pessimistic fetch
or exclusive lock is not implemented. If a consumer later requires SELECT ... FOR UPDATE,
advisory locks, SKIP LOCKED, RLS or provider-specific retry classification, investigate a
separate **ModulithFoundry.EventSourcing.Postgres** adapter. It would depend on the required
shared segment and the native PostgreSQL provider; the shared segment would not depend on it.
Provider adapters should implement concrete, reviewed guarantees, not just expose raw SQL
behind a universal interface. Transaction ownership, lock lifetime/order, isolation, competing
writers, cancellation, rollback and deadlocks require actual provider proofs. Whether a useful
portable seam exists, and how easy it is to extract, remain open. No new package or locking
interface is selected or implemented by this record.

## Deferred capability catalog

These are reminders and extraction candidates, not a promise to implement every option.
Established approaches usually do not need a throwaway feasibility experiment. They still
need executable contract proofs before the library can claim support, particularly when
compatibility, transaction boundaries, recovery or concurrent workers are involved.

| Capability | Remaining implementation and proof obligation |
| --- | --- |
| Historical schema readers and event upcasting | Read old literal fixtures through explicit deterministic upgrade chains; reject unsupported future versions and missing paths; preserve stored history. Existing exact-version registration is not upcasting. |
| Async projections and subscriptions | Define eventual consistency, ordering, tenant scope and poison-event policy; prove restart, duplicate delivery, cancellation and worker takeover. Projection writes and progress must commit atomically within their supported database boundary. |
| Global event feed and safe progress | Define positions, gaps and outstanding transactions before advancing checkpoints. Allocated sequence numbers alone do not establish commit order; prove delayed commits and rollbacks on PostgreSQL. |
| Worker coordination and partitioning | Define stream/tenant ordering, leases or locks, ownership tokens and scaling boundaries; prove competing workers and stale-worker completion. This is separate from ordinary stream optimistic concurrency. |
| Snapshots and snapshot-plus-tail loading | Bind snapshot state to a version and schema; validate the complete tail and captured head; prove equivalence to full reconstruction and behavior for missing/invalid snapshots. Required inline state is not a partially current snapshot. |
| Projection rebuild, repair and revisions | Define privileged admission, shadow/live destinations, concurrent append coordination and cutover; prove failure/restart and continued writer safety. Rebuilding a view does not rewrite historical facts. |
| Multi-stream projections | Keep each projection's state and reducer independent; define grouping, cross-stream order and deletion behavior. Current required projections can provide several views of one stream; they do not establish multi-stream grouping or an engine. |
| Event metadata and provenance | Review causation, correlation, actor/initiator and tenant capture, optional-field compatibility and replay behavior. Business meaning and disclosure policy remain consumer-owned. |
| Command idempotency and ambiguous commit recovery | Define stable operation identities and retained results; prove duplicate submissions and recovery after commit with a lost response. Expected-version checks alone do not identify an already successful command. |
| Additional write coordination and batched operations | Native expected-version append already exists. The current provided store captures and checks a single-stream version without an explicit lock. Review exclusive locking or multi-stream batches separately; prove lock ordering, deadlocks, conflicts and all-or-nothing failure. |
| Stream lifecycle and historical read extensions | Define closure, reopening, tombstones, retention and any additional temporal semantics; preserve historical compatibility and reject unsupported transitions. Current bounded version/time reads are already consumer capabilities. |
| Messaging/outbox and external subscriptions | Separate durable staging from publication; prove duplicate/redelivery and ambiguous external effects. Event sourcing must remain usable without messaging. |
| Audit participation | Define required versus denial audit and payload policy; prove participant failure in the native transaction. Events alone are not an implemented audit integration. |
| Cross-module transactions | Require a concrete workflow and explicit connection/enlistment/ownership contract; prove rollback of every participant. No such runtime contract is supported by ES1. |
| Tenant write tools and database isolation | Review tenant assignment plus mismatch rejection for tracked writes, and coverage for bulk/raw paths. Table configuration may drive optional RLS migration policies (`USING` and `WITH CHECK`), but role/context handling and bypass boundaries require real database tests. An interceptor alone is not database enforcement. |
| Serialization alternatives, generated registration and AOT | Prove retained payload compatibility, unsupported-type errors and an actual selected consumer build. Alternative formats and discovery are optional, not reasons to replace the current explicit JSON registry. |
| Operations, indexing and performance | Define measurable query/append limits, diagnostics and maintenance behavior using realistic payloads/state. Event count alone does not establish throughput, memory use or recovery bounds. |
| Template event compositions | Exercise each selected generated composition, repeat creation and omission guarantees. T1's existing generated composition remains event-free. |

Marten's [versioning documentation](https://martendb.io/events/versioning.html) describes
upcasting at read time. Its [async daemon documentation](https://martendb.io/events/projections/async-daemon.html)
describes progress tracking and a safe high-water boundary, including transaction-related
sequence gaps. These are useful references for later contract design; they do not transfer
their runtime guarantees to our EF implementation.

## Documentation and proposed family layout

Current capabilities, setup, limitations and deferred context belong beside the owning
library. Repository ADRs, plans and slice reports record cross-cutting decisions, owner review
and dated execution evidence; they link to this consumer-facing contract rather than define
an essential behavior only outside the library. Update these local documents with any future
supported interface or proof, and keep proposed features clearly marked.

A possible later repository layout groups related packages and their documentation/tests:

```text
src/ModulithFoundry.EventSourcing/
  README.md
  docs/
  ModulithFoundry.EventSourcing/
  ModulithFoundry.EventSourcing.EntityFrameworkCore/
  tests/
  # ModulithFoundry.EventSourcing.Postgres/ if a concrete provider capability earns it
```

This is a relocation proposal, not the current layout. Evaluate source/project references,
solution entries, architecture tests, CI/hook paths, template source snapshots and historical
links together before moving projects. A family README can describe composition while each
package retains its own dependency/setup contract and can be extracted independently. Shared
repository proofs spanning libraries/consumers may remain in top-level tests. There is no need
to nest EventSourcing under Events or introduce an umbrella runtime dependency.

## Evidence and review boundary

The store follow-up adds one bounded shared write mechanism, explicit tracked native-save
integration and consumer proofs. See [the exact scope](../../../docs/plans/es1-library-write-store.md) and
[execution report](../../../docs/reports/es1-library-write-store.md). Archive/external sources remain
reference evidence. Every deferred capability still needs its own concrete consumer, public
surface, errors, dependencies, reviewed file scope and executable supported-contract proofs.
