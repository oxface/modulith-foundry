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
    internal const string StreamType = "inventory.stock-position";

    internal async Task<StockPositionAggregate> LoadForWritingAsync(
        Guid stockItemId, Guid stockingLocationId, long expectedVersion,
        CancellationToken cancellationToken)
    {
        if (expectedVersion < 0)
        {
            throw new InvalidStockPositionValueException(
                "expectedVersion", "Expected version cannot be negative.");
        }

        StockPositionWriteModel? writeModel = await context.StockPositionWriteModels.AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.StockItemId == stockItemId
                    && candidate.StockingLocationId == stockingLocationId,
                cancellationToken);
        if (writeModel is null)
        {
            if (expectedVersion != 0) { throw new StockPositionConcurrencyException(); }
            return StockPositionAggregate.Empty(Guid.CreateVersion7(timeProvider.GetUtcNow()));
        }

        EventStream stream = await context.EventStreams.SingleAsync(
            candidate => candidate.Id == writeModel.StreamId && candidate.StreamType == StreamType,
            cancellationToken);
        if (stream.Version != expectedVersion) { throw new StockPositionConcurrencyException(); }
        if (writeModel.Version < stream.Version)
        {
            throw new InvalidOperationException(
                "Stock Position write model is behind its stream; rebuild is required.");
        }

        if (writeModel.Version > stream.Version) { throw new StockPositionConcurrencyException(); }

        return StockPositionAggregate.FromState(stream.Id, stream.Version, writeModel.ToState());
    }

    internal async Task<StockPositionAggregate?> LoadLiveAsync(
        Guid streamId, CancellationToken cancellationToken)
    {
        EventStream? stream = await context.EventStreams.AsNoTracking().SingleOrDefaultAsync(
            candidate => candidate.Id == streamId && candidate.StreamType == StreamType,
            cancellationToken);
        if (stream is null) { return null; }

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
        if (aggregate.State is null)
        {
            throw new InvalidOperationException("Cannot append an empty Stock Position.");
        }

        string? traceId = Activity.Current?.TraceId.ToString();
        var metadata = new StockPositionEventMetadata(
            organizationId.Value, actorUserId.Value, traceId, CausationId: null, traceId);

        if (aggregate.ExpectedVersion == 0)
        {
            context.EventStreams.Add(EventStream.Open(
                aggregate.StreamId, organizationId.Value, StreamType, aggregate.Version, recordedAt));
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
            ConstraintName: StockPositionWriteModelConfiguration.IdentityConstraint or "ux_events_stream_version",
        };
}
