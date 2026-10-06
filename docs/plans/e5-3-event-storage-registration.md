# E5.3 explicit event-sourcing storage registration

Status: owner-authorized interface implemented, 2026-10-06. The implementation remains
unstaged for line-by-line review; [the report](../reports/e5-3-event-storage-registration.md)
records fresh proofs and remaining limits. E5.2.2 also remains unstaged, not checkpointed.
No commit is authorized. The approved scope and implementation gate are retained below.
Read [design](../design.md), [the extraction plan](library-extraction.md) and
[the append findings](../reports/e5-2-2-native-event-append.md) alongside it.

## Outcome and evidence

Extract explicit native EF model registration for shared stream/envelope tables. Inventory
and Purchasing should use the library inside their existing native DbContexts, preserving
their current schema, migrations, reads, writes and caller-controlled transactions.
An independent consumer should demonstrate multiple stream types in one table pair without
adopting modules, Tenancy, ActorIdentity, a codec or messaging.

Both active HistoryMapping implementations repeat the same technical mapping: property names,
stream-version concurrency, positive versions, keys, position uniqueness and stream relationship.
Their HistoryRows also repeat the same technical fields plus consumer-owned OrganizationKey.
This is a narrower extraction candidate than generic append orchestration. Native EF supplying
concurrency does not prevent a useful registration utility from removing repeated configuration.

The archive used one event_streams/events pair per module and StreamType on headers, with
module-specific IEntityTypeConfiguration classes registered through native assembly configuration.
Its shared ModelBuilder extension handled ownership filters; no general shared event-table
registration utility was found. Archived global sequences, metadata and repair coordination
are historical evidence, not fields or responsibilities automatically copied into this library.

No new reusable mechanism is proven by this proposal. The implementation must earn that
finding through actual adoption, preserved behavior and the proofs below.

## Proposed package and dependencies

Use one project: **ModulithFoundry.EventSourcing.EntityFrameworkCore**.

Reference Microsoft.EntityFrameworkCore.Relational at the repository's pinned version.
Use native System.Text.Json.JsonElement for the existing payload shape. Do not reference
Events.Serialization, Events.History, Persistence.EntityFrameworkCore, Tenancy, ActorIdentity,
ASP.NET Core, DI, Npgsql, messaging or any sample Contracts.

The package name distinguishes event-sourcing stream storage from integration-event transport
or outbox storage. It does not introduce a general event store. No separate abstractions or
provider project is needed for this capability. Consumers explicitly supply the provider and
its payload mapping; PostgreSQL is the first proven configuration, not a provider-neutral claim.
One native HasColumnType("jsonb") call does not justify an otherwise empty PostgreSQL adapter.

## Public types for review

Consumer-owned EF row classes implement two small technical interfaces. They keep their own
concrete types, extra columns and ownership fields; no row or domain base class is required.
These interfaces describe storage fields, not aggregate/domain-event inheritance contracts.

```csharp
public interface IEventStreamRecord
{
    Guid Id { get; set; }
    string StreamType { get; set; }
    long Version { get; set; }
    DateTimeOffset CreatedAt { get; set; }
    DateTimeOffset UpdatedAt { get; set; }
}

public interface IStoredEventRecord
{
    Guid EventId { get; set; }
    Guid StreamId { get; set; }
    long StreamVersion { get; set; }
    string EventName { get; set; }
    int SchemaVersion { get; set; }
    DateTimeOffset RecordedAt { get; set; }
    JsonElement Payload { get; set; }
}

public sealed record EventSourcingStorageOptions
{
    public string StreamsTable { get; init; } = "event_streams";
    public string EventsTable { get; init; } = "events";
    public string? Schema { get; init; }
}

public static ModelBuilder ConfigureEventSourcingStorage<TStream, TEvent>(
    this ModelBuilder model,
    EventSourcingStorageOptions? options = null)
    where TStream : class, IEventStreamRecord
    where TEvent : class, IStoredEventRecord;

public static ModelBuilder ConfigureEventSourcingStorage<TStream, TEvent>(
    this ModelBuilder model,
    Expression<Func<TStream, object?>> streamKey,
    Expression<Func<TEvent, object?>> eventKey,
    Expression<Func<TEvent, object?>> eventStreamKey,
    EventSourcingStorageOptions? options = null)
    where TStream : class, IEventStreamRecord
    where TEvent : class, IStoredEventRecord;
```

The first overload uses Id, EventId and StreamId keys without tenancy. The second forwards
explicit key selections to native HasKey/HasForeignKey. These are ordinary native EF scalar/
anonymous composite-key expressions, not callbacks executed during requests or a custom
expression language. No requiredTenant delegate belongs in this API: ownership registration
remains a separate explicit consumer choice.

GUID stream/event identities and JsonElement payloads are the initial supported shape, backed
by both active consumers. Arbitrary identity types, payload representations and key conversions
need evidence before widening these interfaces. Consumer-owned concrete rows avoid forcing
Organization, nullable tenant fields or an inheritance hierarchy into every adopter.

## Registration behavior and customization

The utility explicitly registers exactly the two supplied row types in the consumer's model:

- Map the existing snake_case stream/envelope property names, stream-type/name length limits
  of 100/200 and explicitly supplied, non-generated stream/event identities and stream version.
- Set the header Version concurrency token. Do not load headers, compare versions or stamp values.
- Configure selected primary keys and a restrictive event-to-stream relationship against
  that stream primary key. Derive the unique position index from its actual foreign-key
  properties followed by StreamVersion, avoiding a separate redundant index selector.
- Configure positive header/event/schema-version check constraints using the current default
  column names. Zero remains the caller's creation expectation, never a persisted version.
- Map table names and optional schema. A null schema uses the context's native default schema.
  Preserve existing EF-generated key/index names with the default options so current migrations
  and consumer conflict classification remain compatible.

StreamType is stored application metadata that selects an event family. It is not EF
table-per-hierarchy inheritance, a CLR-type registry or permission to discover handlers/codecs.
There is one pair of tables per configured store; families coexist through their StreamType
values. Model registration does not register the families themselves.

Identity is the selected stream primary key, not implicitly StreamType plus Id. In the current
sample, OrganizationKey/Id identifies a stream across all families in that module. Reusing that
identity with another StreamType does not create a second stream. The initial utility requires
the stream key to end in Id and its matching event reference to end in StreamId; an optional
matching ownership prefix may precede them. Event identity ends in EventId with the same prefix.
Prefix matching means corresponding key types/order; consumer ownership property names may
differ between rows. The consumer supplies their meaning; no property named Tenant is assumed.
Broader identity schemes, including type-scoped keys, are outside this first proof.

Consumer-specific key/ownership columns and filters remain visible. Configure them before
registration, then opt into HasTenantOwnership independently where needed. Npgsql/jsonb remains
an explicit consumer payload mapping. Native builders remain available to configure extra
fields, indexes or override defaults. Changing a checked column requires corresponding native
check-constraint SQL; EF does not rewrite SQL fragments when a property is renamed. Replacing
keys/relationships/indexes or disabling version concurrency changes the proven protocol and
is an explicit policy replacement, not another implicit supported option.

Start with distinct ordinary single-table row types. Reject key shapes that do not match the
documented stream/reference/event identity, including ownership-prefix mismatch. Use native
metadata for this check; do not introduce a general expression visitor or model parser.
Do not silently invent shadow ownership fields. Inheritance, owned/shared entities, table
splitting and converted identities have no supported guarantee in this increment.

Argument/model failures happen during model construction. Use ordinary argument or native EF/
InvalidOperationException errors for invalid configuration; no new runtime error hierarchy.
There is no runtime cancellation, transaction or retry API in this package.

## Consumer usage

The existing tenant-owned module usage should shrink to this shape:

```csharp
modelBuilder.HasDefaultSchema("inventory");
var stream = modelBuilder.Entity<EventStream>();
var stored = modelBuilder.Entity<StoredEvent>();
stream.Property(row => row.OrganizationKey)
    .HasColumnName("organization_key").HasMaxLength(256);
stored.Property(row => row.OrganizationKey)
    .HasColumnName("organization_key").HasMaxLength(256);

modelBuilder.ConfigureEventSourcingStorage<EventStream, StoredEvent>(
    streamKey: row => new { row.OrganizationKey, row.Id },
    eventKey: row => new { row.OrganizationKey, row.EventId },
    eventStreamKey: row => new { row.OrganizationKey, row.StreamId });

stream.HasTenantOwnership(row => row.OrganizationKey,
    () => RequiredOrganizationKey, "OrganizationScope");
stored.HasTenantOwnership(row => row.OrganizationKey,
    () => RequiredOrganizationKey, "OrganizationScope");
stored.Property(row => row.Payload).HasColumnType("jsonb");
```

An ordinary consumer without tenants uses the simple overload and its chosen native provider:

```csharp
modelBuilder.ConfigureEventSourcingStorage<EventStreamRecord, StoredEventRecord>(
    new EventSourcingStorageOptions
    {
        Schema = "journal",
        StreamsTable = "streams",
        EventsTable = "facts"
    });
modelBuilder.Entity<StoredEventRecord>().Property(row => row.Payload).HasColumnType("jsonb");
```

The consumer still constructs rows, sets StreamType/identities/timestamps and serializes payloads
deliberately. It registers its native DbContext/provider, owns migrations, starts transactions,
saves/commits/rolls back and disposes. Existing E5.2.2 command decisions, typed outcomes, expected
prefix loading and conflict helpers remain module code. Constraint/table renaming requires the
consumer's constraint-specific conflict policy to use the new names. No automatic collection,
population, DI registration, schema discovery, publication or transaction middleware is added.

## Implementation and proof plan

Implement one reviewable model-registration capability after interface review:

1. Add the one library with inline project references. Adapt both module row types/mappings
   without changing Contracts, domain decisions, existing SQL schema or stored literals.
   Preserve native migration histories; pending-model checks must find no changes. If a native
   model difference appears, resolve/explain it rather than silently introduce a new migration.
2. Add a small executable EventStorageDemo that references only this Foundry segment plus its
   selected EF/provider packages. Use consumer-defined rows and two explicit StreamType values
   in the same table pair. Write/read finite independently expected payloads and versions through
   visible native EF operations. These are standalone proof data, not new Wholesale domain rules.
3. Add focused real PostgreSQL proofs with the existing Testcontainers fixture. Run actual
   consumer-owned migrations; exercise both the tenant-free custom-name model and existing
   tenant-owned sample models. Extend native architecture/dependency checks and the existing
   PostgreSQL CI lane. Keep commit hooks container-free; add no custom graph/parser tooling.

| Proof | Required evidence |
| --- | --- |
| Shared table pair | Two different StreamType values coexist in one pair of tables; consumer predicates return their own independently expected histories. No per-family schema/table is created. |
| Identity boundary | Reusing the same stream identity under another type cannot evade the selected header key. Version positions remain unique per selected stream identity. |
| Relationship and ownership | An envelope cannot point to a missing or differently owned header. Current colliding GUIDs across tenants remain valid and isolated. |
| Customization and independence | A tenant-free executable with custom schema/table names runs without any other Foundry segment. Payload mapping and saves remain visibly native consumer choices. |
| Native version predicate | The configured header token protects against competing prepared native writers; complete header/envelope visibility still depends on the consumer's explicit transaction. |
| Safe configuration failure | Ownership/key mismatches fail at model construction instead of producing an unscoped relationship/index or shadow ownership field. Keep cases about our supported shape, not every EF argument failure. |
| Existing consumer compatibility | Rerun read/append, state-stored HTTP and independent codec/history consumers; existing migrations/snapshots, durable fixtures and conflict classification remain compatible. |

Reuse E5.2.2's writer/fault matrix instead of duplicating every native transaction test. PostgreSQL
tests should inspect only metadata/schema that protects this utility's guarantees. No separate
large in-memory model matrix merely repeats the database proofs or tests native EF mechanics.
Use existing architecture tooling to protect dependencies; no restored-project parser.

Run affected suites, full active build/style/analyzers/formatter, native pending-model checks,
archive integrity and document/whitespace checks. Report fresh results separately from E5.2.2
and archived evidence. The final handoff includes public interfaces, mapping implementation,
both executable usages and remaining limits for line-by-line review; leave everything unstaged.

## Library, template and later findings

Library extraction is proven only if both modules replace their common technical configuration
and the independent/customized consumer preserves the stated guarantees. If the interface
requires more plumbing than the repeated configuration removes, simplify or reject it before
claiming reuse. Consumer-owned rows and keys are intentional; domain/business policy stays out.

Template material is explicit model/provider/ownership registration and native migrations,
with event sourcing optional. Sample findings are preserved behavior in both owning modules
and actual mixed-type storage in the independent consumer. Materialized template output remains E10.

Required views/repair remain E6. Generic append orchestration, snapshots, global sequences,
integration-event outbox, domain-event collection and audit remain separate evidence gates.
No provider-neutral portability, append-only enforcement against privileged SQL, business-key
uniqueness or ambiguous-commit recovery follows from registration utilities.

## Follow-up review: storage names and the DDD/projection scope

The owner requested comparison with Marten and the archived aggregate/projection flows before
settling the wider event-sourcing design. [The reference review](../reports/marten-event-sourcing-reference.md)
records that evidence. This mapping capability remains narrower than an aggregate runtime.

The owner approved proceeding with EventStreamRecord for a persisted stream header,
StoredEventRecord for a persisted event, and matching IEventStreamRecord/IStoredEventRecord
storage interfaces. These names now apply to the standalone consumer and library interfaces.
Existing module concrete type names remain consumer-owned. Record describes the persistence
role without requiring the C# record declaration. Only the standalone migration model's CLR
type names change; its SQL operations, durable columns and constraints remain unchanged.

The consumer chooses its provider payload mapping, but the current interface still requires
JsonElement; it does not support every payload representation. Extra metadata or event-position
columns can be consumer-mapped. Whether either needs a reusable registration utility must be
decided with the actual audit/projection use case, including concurrency and recovery proofs.
No allocation sequence is implicitly a safe committed-event cursor.

E6 must compare accepted-batch bookkeeping, live versus inline loading and explicit required-view
coordination, as well as table configuration. Domain deciders, final-candidate policies and view
definitions stay consumer-owned. A useful common mechanism may still emerge from that comparison;
the current absence of append orchestration does not preclude it.
