# E6.1 inline decision state and required views

Status: next-slice implementation proposal, 2026-10-06. Prepared after the owner requested
the [Marten/archive comparison](../reports/marten-event-sourcing-reference.md). This document
specifies the next reviewable capability; it introduces no implemented projection library or
commit authorization. Read [the extraction plan](library-extraction.md), [design](../design.md)
and [the current append proof](../reports/e5-2-2-native-event-append.md) alongside it.

## Outcome

Ordinary Inventory and Purchasing commands use persisted, version-checked decision state,
without replaying their event history. An accepted batch stages its stream advancement,
events and every required inline view in the caller's native module transaction. Live and
temporal history reads remain separate, explicit operations.

One capability spans the two existing aggregate families:

- Stock Position has one aggregate-shaped inline write view.
- Purchase Order has an aggregate-shaped inline write view and an independently evolved
  summary with line count and total. Its summary does not read the newly proposed aggregate
  state to calculate itself.

The archive already exercises these shapes. Its tests are reference evidence, not proof that
the active implementation supports them. No additional domain operations, issuance workflow,
reservation lifecycle, API routes or messaging are required for this increment.

## Domain and decision ownership

Introduce module-internal StockPositionState and PurchaseOrderState rather than using a public
history-result DTO as decision state. State contains the data the existing receipt/line
decisions need. Evolution applies recorded facts to that state; Contracts map independently
to query/command results. Rich aggregate wrappers are allowed but optional. No DDD base class,
generic decider interface or mandatory pending-event container is proposed.

Separate input/command eligibility from final-candidate validation. The existing command
methods snapshot their input batch, decide typed events, evolve a candidate and validate the
complete candidate before staging any mutations. Include representability of quantities,
line amounts and totals; do not invent a new business limit solely to justify a utility.
An invalid later item or arithmetic failure must leave prior accepted state and pending
changes untouched. Reducers must not call current authorization, read clocks, generate IDs,
publish, audit or run current command eligibility during historical reconstruction.

If a wrapper is used, it captures expected version, accepted state and pending events explicitly;
historical hydration leaves pending events empty. It accepts the complete batch only after
candidate validation succeeds. Otherwise the command can keep this protocol in ordinary local
values. Compare the actual duplication before extracting bookkeeping.

## Consumer Contracts for review

Keep current command Contracts and their Staged/NotFound/Conflict outcomes. Staged continues
to mean a proposal in the caller's context, not durable success. Existing history methods,
including ReadCurrentAsync, continue to reconstruct recorded history; they do not silently
change to reading an inline table.

Propose explicit committed-view query Contracts:

```csharp
public interface IStockPositionQueries
{
    Task<StockPositionHistory?> ReadCurrentAsync(Guid id, CancellationToken cancellationToken);
}

public interface IPurchaseOrderQueries
{
    Task<PurchaseOrderHistory?> ReadCurrentAsync(Guid id, CancellationToken cancellationToken);
    Task<PurchaseOrderSummary?> ReadSummaryAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record PurchaseOrderSummary(
    Guid Id,
    long Version,
    DateTimeOffset RecordedAt,
    string Code,
    string Currency,
    int LineCount,
    decimal Total);
```

Reuse existing full-state DTOs at the public query seam; this does not make them the internal
domain state. A consumer may later rename DTOs with an actual broader Contract review. The new
query interfaces belong to sample module Contracts, never to the technical storage library.
Register these queries explicitly in consumer composition. No HTTP endpoint or automatic DI
discovery is added. The existing StockCatalog remains its separate, state-stored proof and is
not automatically maintained by Stock Position events.

## Native storage and loading

Add module-owned native EF migrations for inventory.stock_position_current,
purchasing.purchase_order_current and purchasing.purchase_order_summary. Each view has an
OrganizationKey/StreamId key, version and recorded timestamp. Explicitly map ownership using
the existing ownership utility and relationships to the module's stream registry. Use native
JsonElement/jsonb mapping for structured state where useful; relational summary columns remain
queryable with native LINQ. Contexts, schemas, migration histories and payload mapping stay
consumer-owned. No shared library view entity or automatic population is proposed.

Command loading reads the registry and required views without tracking mutations. After
checking the caller's expected version against the registry, require each relevant view's
version and recorded timestamp to agree with that observed header. Missing or behind required
views fail as integrity errors. A view ahead of the observed header can result from a competing
commit between reads; classify it as concurrency and discard/retry the operation explicitly in
a fresh context. Do not silently reconstruct or overwrite it during append.

Query composition must likewise account for reads separated by a concurrent commit; an
observed mismatch is not automatically corruption. Document the bounded read behavior rather
than promising a historical database snapshot. Unknown streams return null; a known stream
with a missing required view fails. Tenantless access still fails, and another Organization
cannot observe or modify these views.

Use stream identity for these Contracts. Do not add business-key uniqueness indexes or a
generic get-or-create rule in this increment. The archived lookup-loss limitation remains an
explicit design concern for business-key admission and later repair; a rebuildable view is
not a durable identity registry by itself.

## Explicit staging

Prepare all required candidate states and serialized event/view payloads before changing
tracked state. Stage the header with its observed native original version, immutable event
rows at contiguous positions, and explicitly selected view updates. Each view advances from
its own observed state. A line replacement updates the summary's own per-item amounts and
total rather than reading pending PurchaseOrderState. A proposed result must not expose a
mutable tracked object that later receives events again.

The caller still begins the native transaction, stages, calls SaveChangesAsync, commits or
rolls back and disposes. The existing one-batch-per-stream/context recipe remains explicit.
No SaveChanges interception, hidden save, automatic domain-event dispatch, retries, projector
discovery, generated code or common session identity map is introduced.

Start with concrete module-local projectors/coordinators. After both families work, compare
required-view checks and batch staging against the extraction gate. Propose a callable shared
mechanism only if it removes meaningful complexity while preserving editable view policy.
Do not introduce an empty projection library before that comparison.

## Executable proof and focused failure matrix

Extend EventPersistenceDemo's command journeys to read committed inline state and Purchasing's
summary through Contracts, while retaining the explicit live reads and independently expected
quantities/totals. Existing retained fixture histories need no automatically generated views;
the command-created streams establish views through the actual protocol. Any necessary setup
for older demonstrations remains an explicitly called consumer operation, not migration-time
automatic projection regeneration.

Use the existing real PostgreSQL Testcontainers suite and native observation/fault setup:

- A subsequent receipt/line edit succeeds with event-history reads denied, while explicit live
  reading fails under that denial. This proves the new loading strategy. It does not certify
  all historical facts during an ordinary inline command.
- Two different item lines and replacement of one item produce independently expected line
  count and total. Use literal expectations, not only live/inline reducer equivalence.
- Fail each new required view write; caller rollback leaves the header, all events and every
  required view at their previous committed values. Extend existing append fault cases to
  observe the views instead of duplicating the whole native transaction matrix.
- Missing/behind required views fail before staging; no ordinary append performs repair.
  Coordinate a commit between header/view reads to exercise the ahead-view concurrency path.
- Competing writers leave one complete winning header/event/view batch. Observe durable results
  from a fresh operation scope through query/history Contracts.
- A later invalid item or overflowing candidate leaves no staged proposal or persisted effect;
  the valid retry in a fresh context sees the original version and state.
- Committed-view queries and command loading retain tenant isolation, missing/tenantless guards
  and explicit commit visibility. Extend existing cases where their assertions cover the new
  behavior; avoid another exhaustive EF/framework test matrix.

The existing damaged-history append test assumes every write reconstructs history. That
assumption changes deliberately: explicit live reads continue to reject damaged prefixes,
while inline commands validate their required state and observed header. Document the changed
assurance instead of retaining an incidental history read solely to satisfy the old test.

Verify solution build, affected module/consumer and architecture suites, native pending-model
checks, formatting, semantic style/analyzers, documentation and archive integrity. Report fresh
counts independently of the existing E5 results; do not rerun unrelated browser/broker suites
unless composition actually changes.

## Extraction findings and remaining gaps

Expected library finding: the current storage/ownership/codec/range utilities still compose
with consumer-defined views. No new reusable projection mechanism is proven by this plan.
Any proposed shared coordinator needs its own line-by-line owner review after concrete usage.

Template findings are editable state/decider/projector definitions, explicit registrations,
native migrations and caller-controlled orchestration. Sample findings must demonstrate both
live history and inline decision loading, with independent required views and failure proofs.
Materialized template output remains E10.

E6.2 will separately scope bounded persisted-view repair. Replay alone is not repair. Writer
barriers, original-stream discovery, creation admission and projection-dependent identity must
be reviewed before implementing it. Audit participates only when its actual consumer-owned
staging is added and tested; E6.1 does not claim event/view/audit atomicity. Async projections,
global committed progress, checkpoint snapshots, view revisions, pending-event preview,
online rebuild, cross-module transactions and messaging remain later capabilities.
