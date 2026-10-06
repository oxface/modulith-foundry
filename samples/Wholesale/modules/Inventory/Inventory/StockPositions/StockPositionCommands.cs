using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Events.Serialization;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionCommands(
    InventoryDbContext database,
    StockPositionInlineProjection projection
) : IStockPositionCommands
{
    private static readonly JsonEventCodec<IStockPositionEvent> Codec =
        StockPositionCodec.CreateCodec();

    public Task<StockPositionChangeResult> StageOpenAsync(
        OpenStockPosition request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    )
    {
        var events = StockPositionDecisions.Open(request);
        return StageAsync(
            request.Id,
            request.ExpectedVersion,
            events,
            recordedAt,
            cancellationToken
        );
    }

    public Task<StockPositionChangeResult> StageReceiptsAsync(
        ReceiveStock request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    )
    {
        var events = StockPositionDecisions.Receipts(request);
        return StageAsync(
            request.Id,
            request.ExpectedVersion,
            events,
            recordedAt,
            cancellationToken
        );
    }

    private async Task<StockPositionChangeResult> StageAsync(
        Guid id,
        long expectedVersion,
        IStockPositionEvent[] events,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        string owner = database.RequiredOrganizationKey;
        if (database.Database.CurrentTransaction is null)
            throw new InvalidOperationException(
                "Command staging requires the caller's native transaction."
            );
        if (recordedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Supply a UTC recorded timestamp.", nameof(recordedAt));
        if (database.EventStreams.Local.Any(row => row.Id == id))
            throw new InvalidOperationException(
                "Use one append batch per stream in a fresh operation context."
            );

        EventStream? stream = await database
            .EventStreams.AsNoTracking()
            .SingleOrDefaultAsync(row => row.Id == id, cancellationToken);
        StockPositionCurrentRow? current = null;
        if (expectedVersion == 0)
        {
            if (stream is not null)
                return new StockPositionChangeResult.Conflict();
        }
        else
        {
            if (stream is null)
                return new StockPositionChangeResult.NotFound();
            if (stream.StreamType != StockPositionHistoryReader.StreamType)
                throw new InvalidDataException("The owned stream has an unexpected type.");
            if (stream.Version != expectedVersion)
                return new StockPositionChangeResult.Conflict();
            try
            {
                current = await projection.LoadAsync(stream, cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return new StockPositionChangeResult.Conflict();
            }
            if (recordedAt < stream.UpdatedAt)
                throw new ArgumentOutOfRangeException(
                    nameof(recordedAt),
                    "Recorded time cannot regress."
                );
        }

        long nextVersion = checked(expectedVersion + events.LongLength);
        var state = StockPositionEvolution.Apply(current?.ReadState(), events);
        var proposed = state.ToHistory(id, nextVersion, recordedAt);
        var next = StockPositionCurrentRow.Prepare(owner, id, nextVersion, recordedAt, state);
        SerializedEvent[] encoded = events.Select(Codec.Serialize).ToArray();
        StoredEvent[] rows = encoded
            .Select(
                (envelope, index) =>
                    new StoredEvent
                    {
                        OrganizationKey = owner,
                        EventId = Guid.NewGuid(),
                        StreamId = id,
                        StreamVersion = checked(expectedVersion + index + 1),
                        EventName = envelope.EventName,
                        SchemaVersion = envelope.SchemaVersion,
                        RecordedAt = recordedAt,
                        Payload = envelope.Payload,
                    }
            )
            .ToArray();
        cancellationToken.ThrowIfCancellationRequested();

        // No tracked changes occur until the entire proposal and encoded batch are ready.
        if (stream is null)
        {
            stream = new EventStream
            {
                OrganizationKey = owner,
                Id = id,
                StreamType = StockPositionHistoryReader.StreamType,
                Version = nextVersion,
                CreatedAt = recordedAt,
                UpdatedAt = recordedAt,
            };
            database.EventStreams.Add(stream);
        }
        else
        {
            database.EventStreams.Attach(stream); // Original version remains the caller's expectation.
            stream.Version = nextVersion;
            stream.UpdatedAt = recordedAt;
        }
        database.Events.AddRange(rows);
        projection.Stage(current, next);
        return new StockPositionChangeResult.Staged(proposed);
    }
}
