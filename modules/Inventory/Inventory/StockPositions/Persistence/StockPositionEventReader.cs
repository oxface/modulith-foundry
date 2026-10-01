using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionEventReader(InventoryDbContext context)
{
    internal async Task<StockPositionAggregate?> LoadAtVersionAsync(
        Guid streamId, long? version, CancellationToken cancellationToken)
    {
        EventStream? stream = await FindStreamAsync(streamId, cancellationToken);
        if (stream is null || version > stream.Version) { return null; }
        return await ReplayAsync(stream, version ?? stream.Version, cancellationToken);
    }

    internal async Task<StockPositionAggregate?> LoadAsOfAsync(
        Guid streamId, DateTimeOffset recordedAt, CancellationToken cancellationToken)
    {
        EventStream? stream = await FindStreamAsync(streamId, cancellationToken);
        if (stream is null) { return null; }
        DateTimeOffset cutoff = recordedAt.ToUniversalTime();
        long version = await context.Events.AsNoTracking()
            .Where(stored => stored.StreamId == streamId && stored.StreamVersion <= stream.Version
                && stored.RecordedAt <= cutoff)
            .OrderByDescending(stored => stored.StreamVersion)
            .Select(stored => stored.StreamVersion)
            .FirstOrDefaultAsync(cancellationToken);
        if (version == 0)
        {
            if (cutoff >= stream.CreatedAt)
            {
                throw new StockPositionIntegrityException(
                    streamId, StockPositionIntegrityFailure.HistoryGap, expectedVersion: 1, observedVersion: 0);
            }

            return null;
        }

        return await ReplayAsync(stream, version, cancellationToken);
    }

    private async Task<EventStream?> FindStreamAsync(Guid streamId, CancellationToken cancellationToken)
    {
        EventStream? stream = await context.EventStreams.AsNoTracking().SingleOrDefaultAsync(
            stream => stream.Id == streamId && stream.StreamType == StockPositionStore.StreamType,
            cancellationToken);
        if (stream is { Version: < 1 })
        {
            throw new StockPositionIntegrityException(
                streamId, StockPositionIntegrityFailure.HistoryVersionMismatch,
                expectedVersion: 1, observedVersion: stream.Version);
        }

        return stream;
    }

    private async Task<StockPositionAggregate> ReplayAsync(
        EventStream stream, long version, CancellationToken cancellationToken)
    {
        StoredEvent[] storedEvents = await context.Events.AsNoTracking()
            .Where(stored => stored.StreamId == stream.Id && stored.StreamVersion <= version)
            .OrderBy(stored => stored.StreamVersion)
            .ToArrayAsync(cancellationToken);
        ValidateRange(stream.Id, storedEvents, afterVersion: 0, expectedCount: version);
        try
        {
            IStockPositionEvent[] events = [.. storedEvents.Select(StockPositionEventSerializer.Deserialize)];
            return StockPositionAggregate.Rehydrate(stream.Id, events);
        }
        catch (ArgumentException exception)
        {
            throw new StockPositionIntegrityException(
                stream.Id, StockPositionIntegrityFailure.InvalidEventPayload,
                expectedVersion: version, innerException: exception);
        }
        catch (OverflowException exception)
        {
            throw new StockPositionIntegrityException(
                stream.Id, StockPositionIntegrityFailure.InvalidEventPayload,
                expectedVersion: version, innerException: exception);
        }
        catch (InvalidOperationException exception)
        {
            throw new StockPositionIntegrityException(
                stream.Id, StockPositionIntegrityFailure.InvalidEventSequence,
                expectedVersion: version, innerException: exception);
        }
    }

    internal static void ValidateRange(
        Guid streamId, IReadOnlyList<StoredEvent> events, long afterVersion, long expectedCount)
    {
        for (int index = 0; index < events.Count; index++)
        {
            StoredEvent current = events[index];
            if (current.StreamVersion != afterVersion + index + 1)
            {
                throw new StockPositionIntegrityException(
                    streamId, StockPositionIntegrityFailure.HistoryGap,
                    expectedVersion: afterVersion + index + 1, observedVersion: current.StreamVersion);
            }

            if (index > 0 && current.RecordedAt < events[index - 1].RecordedAt)
            {
                throw new StockPositionIntegrityException(
                    streamId, StockPositionIntegrityFailure.InvalidEventSequence,
                    observedVersion: current.StreamVersion);
            }
        }

        if (events.Count != expectedCount)
        {
            throw new StockPositionIntegrityException(
                streamId, StockPositionIntegrityFailure.HistoryVersionMismatch,
                expectedVersion: afterVersion + expectedCount,
                observedVersion: events.Count == 0 ? afterVersion : events[^1].StreamVersion);
        }
    }

    internal async Task<StockPositionEventPage?> ReadPageAsync(
        Guid streamId, long afterVersion, int limit, CancellationToken cancellationToken)
    {
        EventStream? stream = await FindStreamAsync(streamId, cancellationToken);
        if (stream is null) { return null; }
        StoredEvent[] events = await context.Events.AsNoTracking()
            .Where(stored => stored.StreamId == streamId && stored.StreamVersion > afterVersion
                && stored.StreamVersion <= stream.Version)
            .OrderBy(stored => stored.StreamVersion)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
        long expectedCount = Math.Min(limit, Math.Max(0, stream.Version - afterVersion));
        ValidateRange(streamId, events, afterVersion, expectedCount);
        long? next = events.Length > 0 && events[^1].StreamVersion < stream.Version
            ? events[^1].StreamVersion
            : null;
        return new(stream.Version, events, next);
    }
}

internal sealed record StockPositionEventPage(long Version, IReadOnlyList<StoredEvent> Events, long? NextAfterVersion);
