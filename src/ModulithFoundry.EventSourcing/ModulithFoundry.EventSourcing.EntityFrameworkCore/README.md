# Explicit EF event-sourcing storage

Opt-in model registration for consumer-owned stream/header and durable envelope rows.
The package references EF Core Relational and the package-free EventSourcing aggregate core.
It requires no Npgsql, actor, tenant, codec, hosting or messaging registration.

[Current capabilities and deferred directions](docs/capabilities.md) live in this package,
including provider-specific adapter candidates and projection rebuild/async/multi-stream limits.
Root plans and reports are supplementary review/evidence records.

Implement IEventStreamRecord on your header. For envelopes, use the supplied sealed
StoredEventRecord or your own IStoredEventRecord implementation, then call
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
The default StoredEventRecord supplies common envelope properties without ownership or extra
fields. Its payload can represent different event classes; durable name/schema determines
decoding. Provider JSON mapping and selected codec/direct JSON stay explicit. A custom row
remains appropriate for composite ownership or added fields.

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

The caller owns provider/context registration, migrations, consumer fields, serialization,
stream-type queries, decisions, transactions, saving, commit/rollback, retry and disposal.
Registration neither collects events nor loads histories or
coordinates required views by itself. The opt-in write store below adds native coordination;
no DbContext base, ambient transaction, generated runtime code or publishing worker is added.

PostgreSQL **18.6**, EF **10.0.12** and Npgsql EF **10.0.3** are the proven consumer configuration.
No other DBMS or provider-neutral compatibility is claimed. Check SQL uses the default column
names; payload/provider mapping remains consumer code. Registration cannot protect histories
from privileged SQL updates/deletes, prove business-key uniqueness or resolve ambiguous commits.

## Provided aggregate write store

Handlers depend on `IEventStore<TAggregate>`. The provided
`EventStore<TAggregate, TEvent, TStream, TStoredEvent>` captures stream version internally,
validates family/state metadata and coordinates prepared event/header/required-state changes.
It requires the caller's native transaction and never calls SaveChanges or commit:

```csharp
await using var transaction = await database.Database.BeginTransactionAsync(token);
var aggregate = await store.GetForWritingAsync(request.Id, request.ExpectedVersion, token);
aggregate ??= PurchaseOrderAggregate.Create(request);
aggregate.SetLines(lines);
var staged = await store.AppendAsync(aggregate, token);
await database.SaveChangesAsync(token);
await transaction.CommitAsync(token);
```

Omit expectedVersion to protect the fetched version without a separate command expectation.
A missing stream returns null with a version-zero observation. Domain Create emits its opening
fact immediately. An update can instead report NotFound. EventAppendResult gives staged version
and recorded time, not durability. Empty/rejected decisions need no append. Refetch supersedes
an earlier root; append once per observed stream/context, then discard the operation after
rollback/fault. No pending-event clearing, automatic retry or tracker repair.

A derived binding supplies trusted new-stream ownership through CreateStream. It configures
`InlineAggregateAdapter<TAggregate, TRow>` once for main state: Restore decodes the committed
row and returns an effect-free aggregate; CreateRecord returns a fresh detached state candidate.
The store sets complete foreign keys, version and recorded time. Additional required views use
`InlineEventProjection<TEvent, TRow>` to evolve their own committed state into fresh candidates.
It prepares all participants before tracking events/header/state. Main state is already evolved
by domain operations; the store encodes it without applying pending facts a second time.

Implement `IInlineStateRecord` and map each ordinary row with complete-stream-key PK/FK,
Version as a native concurrency token and RecordedAt. Register main state after native mapping:

```csharp
model.ConfigureRequiredInlineState<EventStream, StoredEvent, PurchaseOrderCurrentRow>(family);
model.ConfigureRequiredInlineState<EventStream, StoredEvent, PurchaseOrderSummaryRow>(
    family, isMainState: false);
```

Use one main and distinct inline row types per family/participant. The explicit declarations
must accompany validation in **both** native save overrides:

```csharp
public override int SaveChanges(bool acceptAllChangesOnSuccess)
{
    this.ValidateEventStreamChanges();
    return base.SaveChanges(acceptAllChangesOnSuccess);
}

public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,
    CancellationToken cancellationToken = default)
{
    this.ValidateEventStreamChanges();
    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}
```

Retain any consumer tenant checks alongside this guard. It detects omitted/altered required
metadata, independent state changes, missing advancing headers/event positions and envelope
mutation/deletion before SQL. It requires an explicit active transaction for registered writes.
Annotations alone do not enforce saving; raw SQL, bulk updates/deletes, external writers,
bypassed overrides and semantic state-body tampering are outside the guarantee.

`InlineProjectionStorage<TStream, TRow>` loads through native global filters using complete FK metadata
and validates captured version/time. Missing/behind state fails with InvalidDataException;
ahead state throws DbUpdateConcurrencyException. Validate also checks a row selected by an
existing native joined query without extra I/O. No general history reader or projector engine.

Raw streams remain supported: override LoadAggregateAsync with the existing captured-history
reader and omit inline registration. The independent counter demonstrates that path with direct
JSON and no tenant/codec/DI dependency. A family can contain multiple registered fact types in
one envelope type and maintain several separately configured projections. Neither TStoredEvent
nor JsonElement appears in the handler interface. JSONB provider mapping and durable codec
registrations remain consumer configuration.

See the [Inventory binding](../../../samples/Wholesale/modules/Inventory/Inventory/StockPositions/StockPositionStore.cs),
[Purchasing binding](../../../samples/Wholesale/modules/Purchasing/Purchasing/PurchaseOrders/PurchaseOrderStore.cs)
and [raw counter binding](../../../samples/EventStorageDemo/CounterStore.cs).
[The exact scope/errors](../../../docs/plans/es1-library-write-store.md) and
[store proof report](../../../docs/reports/es1-library-write-store.md) accompany owner review.

## Lower-level prepared append

`EventAppender<TEvent, TStream, TStoredEvent>` remains the technical batch mechanism inside
the store, configured with the native DbContext, EventRecordAdapter and TimeProvider.
The adapter returns a fresh detached row with durable event identity/schema, payload,
ownership and extra fields. The appender owns event identity/position/time and payload lifetime.
Consumers needing the lower-level path can use Prepare/Stage explicitly; configured native
save checks still require declared inline participants.

The appender requires IEventSourcedAggregate, checks its captured version/identity/pending
count against the observed detached header, and prepares the complete batch without tracking
or changing that header. It generates nonempty distinct GUIDs, assigns contiguous positions
in accepted order, samples GetUtcNow once, clones payloads and validates the complete mapped
foreign key including ownership. Source JsonDocuments may be disposed after Prepare.
Encoding remains the adapter's responsibility; fresh rows, nonblank names (up to 200), positive
schemas and nonnull/defined JSON are required. Adapter errors propagate unchanged.

PreparedEventAppend is a single-use prepared batch bound to the aggregate, header, context
and original transaction. It carries concrete encoded rows privately, not just an identifier
or domain-event handle. Its public outputs are ExpectedVersion, NextVersion and RecordedAt.
Stage verifies the aggregate's version and pending references, header/key and transaction are
unchanged, preserves the observed EF original version, advances the header and adds all rows.
It neither saves nor commits nor stages views. Consumer adapters must not mutate the context,
header or returned rows; arbitrary side effects and mutable facts are unsupported.

The application caller completes the operation explicitly:

```csharp
await using var transaction = await database.Database.BeginTransactionAsync(token);
try
{
    var result = await commands.StageIssuesAsync(request, token);
    if (result is StockPositionChangeResult.Staged)
    {
        await database.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
    }
    else
        await transaction.RollbackAsync(token);
}
catch
{
    await transaction.RollbackAsync(CancellationToken.None);
    throw;
}
```

The provided store associates the loaded aggregate with its observation and transaction.
Raw history decoding/folding and domain admission remain consumer code.
For creation, supply a detached version-zero header template with consumer extra fields.
There is no implicit start-or-append, history read, projector discovery or transaction creation.

Empty append/negative expectation/malformed identity or metadata is an argument error.
Inconsistent aggregate count, changed observation, missing/replaced transaction, repeated stage
or already-tracked key is InvalidOperationException. Version mismatch is native
DbUpdateConcurrencyException, without synthetic Entries. Invalid observed timestamps are
InvalidDataException; non-UTC or regressing clock values are argument errors. Overflow,
cancellation and codec/adapter exceptions retain their native meaning. Equal recorded times
are allowed; the library does not invent a later time.

Stage once per complete stream key per context, including after save. Supported key/model
shapes remain those above, with the native Version token. Converted/shadow keys, manual tracker
clearing/detaching and concurrent context use are unsupported. Different stream keys may share
a native transaction; there is no cross-module transaction guarantee. Model constraints and
consumer save guards remain active. Tracking failures are not an in-memory rollback boundary.

Competing writers fail natively at the observed header predicate or exact creation/position
constraints. EF may execute the position insert before the header update. Consumers classify
only their actual known constraints/concurrency entries; event-ID collisions and unrelated
constraints must not become version conflicts. PostgreSQL is the proved provider.

Discard context and aggregate after persistence failure or rollback; reload and decide again.
No retry/rebase, pending clear, repair, snapshots, async processing, global ordering, ambiguous
commit recovery, messaging or audit integration is included. Required-view selection and final save/commit remain consumer policy; the provided store
coordinates declared participants and native save guards enforce their tracked inclusion.

[Standalone adoption](../../../samples/EventStorageDemo/README.md),
[tenant-owned store](../../../samples/Wholesale/modules/Inventory/Inventory/StockPositions/StockPositionStore.cs),
[aggregate core](../ModulithFoundry.EventSourcing/README.md),
[reviewed scope](../../../docs/plans/es1-bounded-event-append.md),
[proof report](../../../docs/reports/es1-bounded-event-append.md).

## Store vocabulary and projection scope

The store's aggregateState binding restores and encodes the command aggregate's persisted
state. requiredProjections includes that state and every declared secondary inline view.
loadedStreams captures the observed header, root, required rows and native transaction.
ProjectionBinding and LoadedStream are internal coordination details, not domain entities.
All required views advance in the same save/transaction. Several views of one stream are
supported; asynchronous or multi-stream projections and rebuilding/catch-up remain deferred
in [the local capability record](docs/capabilities.md).

Native mapping does not require DbSet properties or a DbContext marker. Use Set<T>() for
queries/additions and Entry(instance) for tracking. Concrete store constructors take their
module's typed context and pass it to the shared base; a common DI scope does not select the
context again. Dependency isolation remains explicit consumer composition and architecture
policy, not a separate module container supplied by this library.
