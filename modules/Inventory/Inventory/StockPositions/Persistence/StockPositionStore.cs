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
    StockPositionEventReader eventReader,
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

        EventStream stream = await context.EventStreams.SingleOrDefaultAsync(
            candidate => candidate.Id == writeModel.StreamId && candidate.StreamType == StreamType,
            cancellationToken) ?? throw new StockPositionIntegrityException(
                writeModel.StreamId, StockPositionIntegrityFailure.StreamMissing, expectedVersion: writeModel.Version);
        if (stream.Version != expectedVersion) { throw new StockPositionConcurrencyException(); }
        if (writeModel.Version < stream.Version)
        {
            throw new StockPositionIntegrityException(
                stream.Id, StockPositionIntegrityFailure.WriteModelBehind,
                expectedVersion: stream.Version, observedVersion: writeModel.Version);
        }

        if (writeModel.Version > stream.Version) { throw new StockPositionConcurrencyException(); }

        return StockPositionAggregate.FromState(stream.Id, stream.Version, writeModel.ToState());
    }

    internal Task<StockPositionAggregate?> LoadLiveAsync(Guid streamId, CancellationToken cancellationToken) =>
        eventReader.LoadAtVersionAsync(streamId, version: null, cancellationToken);

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
            if (recordedAt < stream.UpdatedAt)
            {
                throw new StockPositionIntegrityException(
                    stream.Id, StockPositionIntegrityFailure.RecordedTimeRegression,
                    expectedVersion: stream.Version);
            }

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
