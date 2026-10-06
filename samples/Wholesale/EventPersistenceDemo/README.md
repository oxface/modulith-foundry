# Native event-history consumer

This executable uses the independent Events.Serialization and Events.History libraries in
module-owned native EF readers. Inventory reconstructs stock positions; Purchasing reconstructs
purchase orders. Both use tenant-discriminated rows in separate schemas of one PostgreSQL
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

Expected output:

```text
wholesale-alpha: stock current=13.000, version-2=10.125, cutoff=10.125, before-open=none
wholesale-alpha: order current=62.50, version-2=31.25, cutoff=62.50, before-draft=none
wholesale-beta: stock current=26.000, version-2=20.250, cutoff=20.250, before-open=none
wholesale-beta: order current=125.00, version-2=62.50, cutoff=125.00, before-draft=none
```

Setup skips already-present streams for an ordinary sequential rerun. It has no concurrent
bootstrap or expected-version append guarantee. Use a disposable database; demonstration
setup is not account admission, business-key discovery or a production command implementation.
Each module save owns its native transaction; there is no cross-module atomic setup.

## Read contract

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

[The PostgreSQL suite](../EventPersistenceDemo.Tests/HistoryReadTests.cs) runs actual migrations,
launches this executable and checks independently expected module results. Native EF command
interception observes SQL and coordinates a commit between header capture and event selection;
no production test hook is present. A later append is excluded from that captured read and
included by a fresh read. The controlled writer commits header/events together; production
append protocol proof remains E5.2.2.

Ordinary read consistency assumes atomically committed, append-only histories. Arbitrary
privileged updates/deletes can invalidate that assumption; selected-range success does not
certify excluded rows. Full replay is unbounded in history length and no snapshot or capacity
claim is made. The as-of query's index/performance needs depend on actual workloads.

The availability catalog remains a separate state-stored Inventory demonstration. This
increment does not make it an event projection. Required atomic views/repair remain E6 and
reliable messaging E7. Provider-specific jsonb mapping is sample code; other DBMSs are unproven.

[Composition](DemoComposition.cs), [executable recipe](DemoJourneys.cs),
[module ownership](../modules/README.md), [plan](../../../docs/plans/e5-2-1-native-event-history.md).
Materialized template output and bootstrap CLI remain E10.
