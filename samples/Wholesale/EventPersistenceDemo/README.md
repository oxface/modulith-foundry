# Native event-history, append and inline-view consumer

This executable uses the independent Events.Serialization and Events.History libraries in
module-owned native EF readers and explicit command writers. Inventory reconstructs stock
positions and stages receipts/issues; Purchasing reconstructs orders and stages line changes.
Both use tenant-discriminated rows in separate schemas of one PostgreSQL
connection, with separate native migration histories. No messaging registration is needed.

```sh
export WHOLESALE_DEMO_CONNECTION_STRING='<disposable PostgreSQL connection string>'
dotnet run --project samples/Wholesale/EventPersistenceDemo/EventPersistenceDemo.csproj
```

The caller registers contexts/providers and tenancy explicitly, migrates each module, establishes
one immutable tenant scope per Organization, stages finite authored histories and saves through
native EF. A stock position and purchase order share the same stream GUID across two tenants;
Beta's authored quantities are twice Alpha's. The six durable JSON literals come from
[EventCodecDemo](../EventCodecDemo/Fixtures/README.md), unchanged. Recorded timestamps and the
quantity multiplier are demo metadata/policy, not library behavior.

The second journey opens/drafts different streams through business command Contracts, then
stages and commits a two-event batch for each. The caller visibly begins each native transaction,
saves, commits or rolls back, and disposes the operation scope. Fresh-context reads report the
committed result. No command independently saves, retries or publishes.

Expected output:

```text
wholesale-alpha: stock current=13.000, version-2=10.125, cutoff=10.125, before-open=none
wholesale-alpha: order current=62.50, version-2=31.25, cutoff=62.50, before-draft=none
wholesale-beta: stock current=26.000, version-2=20.250, cutoff=20.250, before-open=none
wholesale-beta: order current=125.00, version-2=62.50, cutoff=125.00, before-draft=none
inventory append: committed-version=3, on-hand=13.000
purchasing append: committed-version=3, total=62.50
inventory inline: committed-version=3, on-hand=13.000
purchasing inline: committed-version=3, total=62.50
purchasing summary: committed-version=3, lines=1, total=62.50
inventory issues: committed-version=5, on-hand=6.000, rejected=7
```

Setup skips already-present streams for an ordinary sequential rerun. It has no concurrent
bootstrap guarantee. Command setup also skips completed streams on a sequential rerun;
the command protocol below provides expected-version protection for its explicit identity.
Use a disposable database; demonstration
setup is not account admission or business-key discovery. There is no cross-module atomic setup.

## Command contract

`AddStockPositionCommands()` and `AddPurchaseOrderCommands()` explicitly bind the internal
implementations after the caller registers the native contexts. History registration is needed
for explicit replay, not for command loading. Open/draft
requires expected version 0; receipts/line changes require a positive expected version and a
nonempty valid batch. The established tenant supplies ownership; DTOs carry no tenant or actor.

`StageIssuesAsync(IssueStock, ...)` validates all positive quantities, loads the required
inline state at the requested version and accepts only a complete batch whose sum fits
current OnHand. InsufficientStock carries Available/Requested and leaves no staged rows.
Inventory owns this rule: stock leaving one position, with no reservation, catalog mutation
or cross-module sale. Historical evolution subtracts issued facts without applying current
eligibility or authorization again.

Both modules inject `IEventStore<TAggregate>` and configure the provided EventStore base once.
Handlers use GetForWritingAsync, domain Create/operations and AppendAsync; bindings no longer
repeat lookup/family/version/transaction orchestration. The library owns those validations,
identities, positions, one batch clock sample and aggregate bookkeeping.
Domain decisions, pure reducers, codec registration, required views and native completion
remain explicit consumer code. [StockIssueJourney](StockIssueJourney.cs) uses a separate stream:
load 13 at version 3, issue 4 and 3 to reach 6 at version 5, then reject issue 7 in a fresh scope.

Commands return Staged, NotFound or Conflict; issues also return InsufficientStock. Staged contains proposed business state, not
durable success. A competitor can still win before saving. Native EF preserves the original
header version in its UPDATE predicate; owned stream keys arbitrate competing creation.
Native composition helpers classify only stream/required-view concurrency and the known PostgreSQL
header/position unique constraints. Other faults retain their actual meaning.

Configure TimeProvider once; the library records one UTC timestamp per batch, without regression.
Module registration uses TimeProvider.System when no clock is supplied; this finite demo configures
a scoped deterministic clock. Business command Contracts take no timestamp. Stage at most one batch per
stream in an operation context; tracked headers detect repeats before and after save. Manual
clearing/detaching tracking mid-operation is outside this recipe. Different
streams may participate in the same module transaction. On failure, roll back and discard the
entire context/proposal; a retry requires a fresh scope and an explicit new decision. An ambiguous
commit error does not establish rollback or safe retry. Caller-controlled commit is the success
boundary. [AppendJourneys](AppendJourneys.cs) makes that ownership visible.

## Inline state and required views

Register `AddStockPositionQueries()` / `AddPurchaseOrderQueries()` explicitly to read committed
inline state. Inventory owns stock_position_current. Purchasing owns purchase_order_current
and purchase_order_summary, whose per-item amounts evolve independently from accepted events.
The caller's append transaction saves headers, events and all required views together.
Explicit model declarations and both native save overrides validate required tracked
participants/metadata/event ranges; a detached required row cannot silently leave events alone.
This contract excludes bypass SQL/bulk saves and does not validate a manually altered state body.
ReadAvailableAsync uses StockPositionFilters over mapped onHand state and scoped native joins,
with server-side filtering rather than historical payload replay.
The availability catalog remains a separate state-stored demonstration.

Commands read module-internal decision state from the inline write view and check every required
view against the observed header. Missing/behind views or mismatched timestamps fail before
staging; append never repairs them. A view ahead of the header observed earlier is a concurrency
outcome, since a competitor can commit between reads. Command loading returns Conflict;
view queries throw native DbUpdateConcurrencyException. Queries do not silently retry or promise
a database snapshot across their separate reads.

Complete candidate evolution, arithmetic validation, event encoding and view payload preparation
finish before tracked rows change. Purchasing validates its final total after the complete batch;
its summary applies the batch to its own committed amounts. The aggregate base collects only
new accepted facts; historical reconstruction collects none. Existing pure reducers are shared
within each module. No discovered projector registry is added.

Inline commands validate their decision state and required view positions, rather than certifying
all historical facts on each edit. Privileged historical corruption can therefore leave an inline
command usable while explicit replay fails. Stop affected writes and investigate known corruption;
this slice adds no automatic corruption discovery or repair. Consumers retain that admission policy.

## Live and temporal read contract

The host calls only business Contracts. Module implementations require established tenancy
and use named EF ownership filters on both headers and events. Missing/foreign streams return
null. Current reads capture a positive committed header version; at-version reads require a
positive version within that head. Before-first time reads return null; time means recorded
time rather than event occurrence or commit order.

Native queries select the maximum qualifying version for an intermediate cutoff, then fetch
its complete ordered prefix. A cutoff at/after header update time targets the full captured
head so a missing tail cannot appear as an earlier state. Selection, ordering and ownership
predicates execute in PostgreSQL before materialization. Equal recorded timestamps preserve
version order.

The reader explicitly validates positions, checks header/event timestamp consistency, decodes
and evolves consumer-owned state. It saves nothing and has no publisher, event handler or
pending-event collection. Purchasing results wrap reconstructed lines in a read-only collection.

Error boundaries remain visible: argument errors for invalid versions, EventHistoryException
for selected-range integrity, InvalidDataException for header inconsistency, EventDecodingException
for codec failures and InvalidOperationException for invalid domain sequences/values. Native
database faults and cancellation propagate. No retry or exception-to-null fallback is added.
These are consumer error policies, not a new Foundry error protocol or HTTP response policy.

## Scope and proofs

[The read suite](../EventPersistenceDemo.Tests/HistoryReadTests.cs) runs actual migrations,
launches this executable and checks all ten output lines. Native EF command
interception observes SQL and coordinates a commit between header capture and event selection;
no production test hook is present. A later append is excluded from that captured read and
included by a fresh read. [The append suite](../EventPersistenceDemo.Tests/AppendTests.cs)
proves competing prepared writers, tenant isolation, visibility only after commit, header/
envelope faults, rollback across two saves and fresh-context recovery. Deterministic races
prepare both writers at the same head before saving the winner; no timing sleeps are used.
Cancellation proof is before commit dispatch, not recovery from an ambiguous in-flight commit.

[Inline-view proofs](../EventPersistenceDemo.Tests/AppendTests.InlineViews.cs) deny actual PostgreSQL
event SELECT permission while allowing editing, check independent two-item totals/replacement,
reject missing/lagging/mistimed views, coordinate a commit between header/view reads and reject
invalid/overflowing batches before tracking. The existing append matrix also observes committed
views and fails each new view participant. This is consumer protocol coverage, not another EF
feature matrix.

[State-dependent proofs](../EventPersistenceDemo.Tests/AppendTests.StateDependent.cs) check
whole-batch issue eligibility, zero-stock completion, isolated tenants, stale/damaged/ahead
observations, competing eligible decisions, required-view/event faults and fresh-context
redecision. The existing E5/E6 matrices retain all original assertions through shared append.

Ordinary read consistency assumes atomically committed, append-only histories. Arbitrary
privileged updates/deletes can invalidate that assumption; selected-range success does not
certify excluded rows. Full replay is unbounded in history length and no snapshot or capacity
claim is made. The as-of query's index/performance needs depend on actual workloads.

The availability catalog remains a separate state-stored Inventory demonstration. This
increment does not make it an event projection. Required inline views are now implemented;
bounded repair remains E6.2, audit and reliable messaging remain later participants. The new
migrations create empty views; they do not automatically populate them for existing streams.
The finite seed now prepares matching main/summary state from the unchanged literals and
saves each module in its own explicit native transaction. Command-created streams establish
state through the provided store. Historical reconstruction remains available separately. Provider-specific jsonb mapping is sample code; other DBMSs are unproven.

[Composition](DemoComposition.cs), [fixture recipe](DemoJourneys.cs),
[module ownership](../modules/README.md), [append findings](../../../docs/reports/e5-2-2-native-event-append.md),
[inline-view findings](../../../docs/reports/e6-1-inline-decision-state.md).
See [the store report](../../../docs/reports/es1-library-write-store.md) for fresh save-boundary
omission/corruption, preparation-conflict, query translation and recovery proofs. T1 already
provides an event-free generated composition; event template presets remain deferred.
