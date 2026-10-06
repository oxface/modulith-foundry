# Explicit EF event-sourcing storage

Opt-in model registration for consumer-owned stream/header and durable envelope rows.
The package references EF Core Relational only. It requires no other Foundry segment,
Npgsql, actor, tenant, codec, hosting or messaging registration.

Implement IEventStreamRecord and IStoredEventRecord on your own concrete EF row classes, then call
the utility from your DbContext's OnModelCreating:

```csharp
modelBuilder.ConfigureEventSourcingStorage<EventStreamRecord, StoredEventRecord>(
    new EventSourcingStorageOptions
    {
        Schema = "journal",
        StreamsTable = "streams",
        EventsTable = "facts"
    });

// Provider mapping is deliberately visible in this PostgreSQL consumer.
modelBuilder.Entity<StoredEventRecord>().Property(row => row.Payload).HasColumnType("jsonb");
```

The simple overload selects Id/EventId primary keys and a StreamId reference without tenancy.
Options default to event_streams/events and the context's native default schema. One pair of
tables supports different StreamType values; this is application metadata, not EF inheritance
or automatic event-family registration. Identical stream identities cannot be reused merely
by selecting a different type.

For scoped identities, use the native key-expression overload:

```csharp
modelBuilder.ConfigureEventSourcingStorage<EventStreamRecord, StoredEventRecord>(
    streamKey: row => new { row.AccountKey, row.Id },
    eventKey: row => new { row.AccountKey, row.EventId },
    eventStreamKey: row => new { row.AccountKey, row.StreamId });
```

Configure your ownership properties/column names yourself. Apply filters and any write guards
explicitly, using native EF or the separate ownership utility. The registration does not
interpret AccountKey as a tenant, resolve it, stamp it or grant access. Corresponding ownership
prefixes may use different names on stream and event rows; their types/order must match, and
the event primary key and stream reference must use the same prefix on the event row.

Registration maps common snake_case columns, 100/200 stream-type/event-name limits, explicitly
supplied GUID identities, the header version concurrency token, positive version constraints,
a restrictive relationship to the selected header primary key and a unique foreign-key-plus-
StreamVersion index. Foreign references or duplicate positions fail through native database
exceptions. Header update races use native EF concurrency, not a library error protocol.

Supported keys end in Id/EventId/StreamId, optionally preceded by matching unconverted CLR
ownership properties. Unsupported/mismatched identities fail during model construction.
Consumer row types remain independent ordinary entities. Inheritance, owned/shared rows,
table splitting, converted keys, arbitrary identity/payload types and type-scoped keys are
outside the initial supported shape.

Native builders remain available for extra columns/indexes or replacing defaults. A checked
column rename requires updating its native check-constraint SQL. Replacing keys, concurrency
or relationships changes the documented guarantee; it is consumer policy. A new table/constraint
name also requires corresponding updates to consumer constraint-specific error classification.

The caller owns provider/context registration, migrations, value population, serialization,
stream-type queries, decisions, expected-version preparation, transactions, saving, commit/
rollback, retry and disposal. Registration neither collects events nor loads histories or
coordinates required views. No DbContext base, store/repository, ambient transaction, generated
runtime code, publishing worker or lifecycle bookkeeping is added.

PostgreSQL **18.6**, EF **10.0.12** and Npgsql EF **10.0.3** are the proven consumer configuration.
No other DBMS or provider-neutral compatibility is claimed. Check SQL uses the default column
names; payload/provider mapping remains consumer code. Registration cannot protect histories
from privileged SQL updates/deletes, prove business-key uniqueness or resolve ambiguous commits.

[Standalone usage](../../samples/EventStorageDemo/README.md),
[tenant-owned module usage](../../samples/Wholesale/modules/Inventory/Inventory/StockPositions/HistoryMapping.cs),
[interface and scope](../../docs/plans/e5-3-event-storage-registration.md),
[proof report](../../docs/reports/e5-3-event-storage-registration.md).
