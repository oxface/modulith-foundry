# Native event-history, append and inline-view consumer

This executable uses the independent Events.Serialization and Events.History libraries in
module-owned native EF readers and explicit command writers. Inventory reconstructs stock
positions and stages receipts; Purchasing reconstructs orders and stages line changes.
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

Commands return Staged, NotFound or Conflict. Staged contains proposed business state, not
durable success. A competitor can still win before saving. Native EF preserves the original
header version in its UPDATE predicate; owned stream keys arbitrate competing creation.
Native composition helpers classify only stream/required-view concurrency and the known PostgreSQL
header/position unique constraints. Other faults retain their actual meaning.

Supply one UTC recorded timestamp per batch, without regression. Stage at most one batch per
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
The availability catalog remains a separate state-stored demonstration.

Commands read module-internal decision state from the inline write view and check every required
view against the observed header. Missing/behind views or mismatched timestamps fail before
staging; append never repairs them. A view ahead of the header observed earlier is a concurrency
outcome, since a competitor can commit between reads. Command loading returns Conflict;
view queries throw native DbUpdateConcurrencyException. Queries do not silently retry or promise
a database snapshot across their separate reads.

Complete candidate evolution, arithmetic validation, event encoding and view payload preparation
finish before tracked rows change. Purchasing validates its final total after the complete batch;
its summary applies the batch to its own committed amounts. No aggregate base, automatic event
collection or discovered projector registry is required.

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
launches this executable and checks all nine output lines. Native EF command
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

Ordinary read consistency assumes atomically committed, append-only histories. Arbitrary
privileged updates/deletes can invalidate that assumption; selected-range success does not
certify excluded rows. Full replay is unbounded in history length and no snapshot or capacity
claim is made. The as-of query's index/performance needs depend on actual workloads.

The availability catalog remains a separate state-stored Inventory demonstration. This
increment does not make it an event projection. Required inline views are now implemented;
bounded repair remains E6.2, audit and reliable messaging remain later participants. The new
migrations create empty views; they do not automatically populate them for existing streams.
The authored fixture streams are live-read demonstrations, while command-created streams establish
their views through actual append. Provider-specific jsonb mapping is sample code; other DBMSs are unproven.

[Composition](DemoComposition.cs), [fixture recipe](DemoJourneys.cs),
[module ownership](../modules/README.md), [append findings](../../../docs/reports/e5-2-2-native-event-append.md),
[inline-view findings](../../../docs/reports/e6-1-inline-decision-state.md).
Materialized template output and bootstrap CLI remain E10.
