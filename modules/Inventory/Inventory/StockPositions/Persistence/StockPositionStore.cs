using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;
using Npgsql;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionStore(
    InventoryDbContext context,
    StockPositionInlineProjection projection,
    TimeProvider timeProvider)
{
    internal const string IdentityConstraint = "ux_stock_position_stream_identity";

    internal async Task<StockPositionAggregate> LoadForWritingAsync(
        Guid stockItemId, Guid stockingLocationId, long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion < 0)
        {
            throw new InvalidStockPositionValueException(
                "expectedVersion", "Expected version cannot be negative.");
        }

        EventStream? stream = await context.EventStreams.SingleOrDefaultAsync(
            candidate => candidate.StreamType == EventStream.StockPositionStreamType
                && candidate.StockItemId == stockItemId
                && candidate.StockingLocationId == stockingLocationId,
            cancellationToken);
        if (stream is null)
        {
            if (expectedVersion != 0) { throw new StockPositionConcurrencyException(); }
            return StockPositionAggregate.Empty(Guid.CreateVersion7(timeProvider.GetUtcNow()));
        }

        if (stream.Version != expectedVersion) { throw new StockPositionConcurrencyException(); }

        StoredEvent[] storedEvents = await context.Events.AsNoTracking()
            .Where(stored => stored.StreamId == stream.Id && stored.StreamVersion <= stream.Version)
            .OrderBy(stored => stored.StreamVersion).ToArrayAsync(cancellationToken);
        for (int index = 0; index < storedEvents.Length; index++)
        {
            if (storedEvents[index].StreamVersion != index + 1L)
            {
                throw new InvalidOperationException("Stock Position stream event versions are not contiguous.");
            }
        }

        IStockPositionEvent[] events = [.. storedEvents.Select(StockPositionEventSerializer.Deserialize)];
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(stream.Id, events);
        if (aggregate.Version != stream.Version)
        {
            throw new InvalidOperationException("Stock Position stream metadata version is inconsistent.");
        }

        return aggregate;
    }

    internal async Task StageAppendAsync(
        OrganizationId organizationId, UserId actorUserId, StockPositionAggregate aggregate,
        DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId.Value, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(actorUserId.Value, Guid.Empty);
        StockPositionState state = aggregate.State
            ?? throw new InvalidOperationException("Cannot append an empty Stock Position.");
        string? traceId = Activity.Current?.TraceId.ToString();
        var metadata = new StockPositionEventMetadata(
            organizationId.Value, actorUserId.Value, traceId, CausationId: null, traceId);

        if (aggregate.ExpectedVersion == 0)
        {
            context.EventStreams.Add(EventStream.Open(
                aggregate.StreamId, organizationId.Value, state.StockItemId,
                state.StockingLocationId, aggregate.Version, recordedAt));
        }
        else
        {
            EventStream stream = await context.EventStreams.SingleAsync(
                candidate => candidate.Id == aggregate.StreamId, cancellationToken);
            stream.Advance(aggregate.ExpectedVersion, aggregate.Version, recordedAt);
        }

        long streamVersion = aggregate.ExpectedVersion;
        foreach (IStockPositionEvent @event in aggregate.UncommittedEvents)
        {
            SerializedStockPositionEvent serialized = StockPositionEventSerializer.Serialize(@event);
            context.Events.Add(StoredEvent.Create(
                Guid.CreateVersion7(recordedAt), organizationId.Value, aggregate.StreamId,
                ++streamVersion, serialized.EventName, serialized.SchemaVersion, recordedAt,
                serialized.Payload, metadata.ToJson()));
        }

        await projection.StageAsync(
            organizationId.Value, aggregate.StreamId, aggregate.ExpectedVersion,
            aggregate.UncommittedEvents, recordedAt, cancellationToken);
    }

    internal static bool IsConcurrencyConflict(DbUpdateException exception) =>
        exception is DbUpdateConcurrencyException
        || exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: IdentityConstraint or "ux_events_stream_version" or "ux_stock_position_current_identity",
        };
}
