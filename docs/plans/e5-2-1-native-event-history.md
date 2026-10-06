# E5.2.1 native EF event-history reads

Status: implementation authorized by the owner on 2026-10-06 after E5.1 checkpoint
`4cc12a1`; implemented changes remain unstaged for line-by-line review. No commit is authorized.
[The implementation report](../reports/e5-2-1-native-event-history.md) records verification and limits.
Read [the extraction plan](library-extraction.md), [design](../design.md) and
[E5.1 findings](../reports/e5-1-event-history.md) alongside this proposal.

## Outcome and increment boundary

Prove that the explicit select/materialize/validate/decode/hydrate recipe works with native
EF queries over real tenant-owned PostgreSQL histories in two independently owned modules.
This exercises the History utility in persistence before deciding its final package division.

Split E5.2 into reads/mappings here and append/transactions in E5.2.2. Finite consumer setup
will stage authored histories and save/commit explicitly. Those setup writes establish test
data, not a production append contract. Expected-version commands, competing writers,
rollback and recovery remain the next increment. No new reusable mechanism is proven by
this proposal.

## Module and consumer ownership

- Inventory owns stock-position event definitions, codec registration, evolution and history
  queries. Extend its existing native DbContext and schema `inventory` with stream/event rows.
  Its availability catalog remains a separate state-stored demonstration; this increment does
  not establish that table as a projection of event history.
- Purchasing owns purchase-order definitions, codec registration, evolution and history
  queries. Introduce populated Purchasing and Purchasing.Contracts projects with its own
  native DbContext, schema `purchasing` and migration history.
- Contracts expose business identities and immutable reconstructed results through actual
  query operations. They expose no EF rows, JSON envelopes, technical actor/tenant types or
  peer module implementations. Queries use established tenancy, not a caller-selected owner.
- A finite executable consumer registers both native contexts and selected libraries,
  migrates explicitly, establishes tenancy, stages fixture histories, saves explicitly and
  reads both modules through Contracts. It needs no transport or messaging registration.

Use the E4/E5.1 durable fixtures as compatibility evidence when implementing these event
families in their owning modules. The small EventCodecDemo remains an isolated library
adoption proof; it is not the authoritative module implementation. Retain archived fixtures
unchanged. Document module language and ownership with the new behavior; add no shared
technical domain-event base or generic aggregate repository.

## Native mappings and migration policy

Each module maps its own stream header and durable event envelope explicitly. Headers
contain tenant owner, stream identity/type, positive committed version and creation/update
timestamps. Events contain owner, event/stream identities, positive stream position, durable
event name/schema version, recorded timestamp and native JSON payload. Do not add global
sequence, dispatch state or a generic metadata bag without a demonstrated reader need.

Use ordinary EF configuration, the existing explicit tenant-ownership utility and native
same-tenant relationship constraints. The database must prevent an event from referencing
a stream owned by another tenant and duplicate positions within one owned stream. Review
keys, indexes, positive-value constraints and JSON mapping as consumer code. The key design
should allow the same stream identity in different tenants so isolation is exercised with
colliding identities, rather than relying solely on globally unique GUIDs.

Add a forward Inventory migration preserving existing availability rows and a native initial
Purchasing migration. Both migrations and model snapshots are consumer-owned and reviewed.
Use the already-approved development-time EF migration scaffolding; registration, migration
execution, provider choice and tenant values remain explicit. Raw SQL and privileged filter
bypasses retain their existing consumer responsibility.

## Read protocol and error boundaries

Implement current, at-version and as-of reads with native IQueryable predicates and ordering
before materialization. Keep query policy in each module; introduce no Foundry query DSL.

1. Read the tenant-scoped header without tracking and capture its committed head. Missing or
   foreign streams return no result. Invalid persisted header metadata is an integrity fault.
2. Current reads target that head. Version reads require a positive requested version within
   the captured head; reject invalid/future requests instead of silently clamping them.
3. Time reads use recorded time, not event occurrence time or commit time. Select the highest
   qualifying version within the captured head, then load the complete ordered prefix through
   that version. Equal timestamps participate in version order; before-first reads yield no
   historical state.
4. Materialize only the selected range, validate positions with EventHistory.ValidateRange,
   decode those rows with the module's codec, then invoke its consumer-owned evolution.
   Reads save nothing and replay triggers no external effects.

Review header consistency explicitly: positive head, ordered creation/update times,
creation matching the first event, and update time matching the last event when the selected
range reaches the captured head. A missing prefix/tail must not be mistaken for a legitimate
before-first result. In particular, a cutoff at/after the header's update time targets the
captured head rather than trusting only the remaining timestamp-filtered rows.

Retain separate argument, range-integrity, codec, domain-sequence and native database/
cancellation errors. No blanket exception wrapping or retries. Selected-range validation
does not certify excluded rows; full-stream maintenance remains separate.

A captured head bounds a later query while another transaction commits additional rows.
This assumes atomically committed, append-only histories. Here, a controlled native setup
writer can exercise that read boundary. It does not prove the production writer enforces
atomicity or immutability; E5.2.2 must establish its append contract. Do not promise a snapshot
against arbitrary privileged history updates or deletions.

## Relevant executable proofs

Use the existing real PostgreSQL Testcontainers infrastructure and actual migrations.
Protect consumer guarantees instead of adding tests for basic EF or .NET behavior:

- Independently expected stock quantities and purchase-order replacement totals for current,
  earlier-version, inclusive equal-time and before-first reads in both module consumers.
- Two tenants with colliding stream identities and differing payloads; reads return only the
  current owner's state, and the actual schema rejects cross-owner stream/event references.
  Missing/tenantless context rejects ordinary queries.
- Inventory migration preserves existing catalog rows; independent Purchasing migration
  leaves Inventory data intact. Reuse existing migration fixtures/patterns where practical.
- A controlled commit after header capture is excluded from that read, and a fresh read sees
  it. Coordinate the race deterministically; avoid timing-based sleeps and production hooks
  introduced solely for tests.
- Selected history gaps/missing tails, recorded-time regression, unknown/invalid payloads and
  header/event timestamp mismatch fail at the intended boundary. Earlier reads remain valid
  when corruption is outside their selected range. Inject impossible persisted states only
  through explicit privileged fault setup.
- Inspect executed SQL for server-side tenant/stream/version/time selection and ordering;
  avoid full SQL snapshots. Add only the integration assertions needed to protect this recipe,
  retaining E5.1's validator tests instead of duplicating their full matrix.
- Extend existing architecture rules to Purchasing and its Contracts; preserve standalone
  event-library adoption and the existing state-stored HTTP consumer behavior.

Run affected PostgreSQL/module/compatibility/architecture checks, the executable journey,
native build/style/analyzers and archive integrity. Report new results separately from
archived reader evidence and E5.1's memory-only results. No provider-neutral database claim
follows from the PostgreSQL implementation.

## Extraction and later gates

The library candidate remains selected-range integrity, exercised here alongside independent
JSON serialization. Propose no new public Foundry interface in this increment. If actual
mapping/query work reveals a valuable repeated protocol, document it for review rather than
silently expanding the slice. Reassess the separate History package after both real readers.

The template gains editable module mappings, migrations, explicit registration and native
read recipes. The sample gains actual module-owned event-history queries. Materialized
template output and configurable bootstrap remain E10.

E5.2.2 defines caller-controlled expected-version staging and native saving/transactions,
with competing append, stream/event-write faults, rollback and fresh-context recovery.
Required atomic views and repair remain E6; reliable messaging remains E7. These capabilities
are not inferred from successful reads or setup writes.
