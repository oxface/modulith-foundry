using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Events.Serialization;
using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderCommands(
    PurchasingDbContext database,
    IPurchaseOrderHistory history
) : IPurchaseOrderCommands
{
    private static readonly JsonEventCodec<IPurchaseOrderEvent> Codec =
        PurchaseOrderCodec.CreateCodec();

    public Task<PurchaseOrderChangeResult> StageDraftAsync(
        DraftPurchaseOrder request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    )
    {
        var events = PurchaseOrderDecisions.Draft(request);
        return StageAsync(
            request.Id,
            request.ExpectedVersion,
            events,
            recordedAt,
            cancellationToken
        );
    }

    public Task<PurchaseOrderChangeResult> StageLinesAsync(
        ChangePurchaseOrderLines request,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    )
    {
        var events = PurchaseOrderDecisions.Lines(request);
        return StageAsync(
            request.Id,
            request.ExpectedVersion,
            events,
            recordedAt,
            cancellationToken
        );
    }

    private async Task<PurchaseOrderChangeResult> StageAsync(
        Guid id,
        long expectedVersion,
        IPurchaseOrderEvent[] events,
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
        PurchaseOrderHistory? current = null;
        if (expectedVersion == 0)
        {
            if (stream is not null)
                return new PurchaseOrderChangeResult.Conflict();
        }
        else
        {
            if (stream is null)
                return new PurchaseOrderChangeResult.NotFound();
            if (stream.StreamType != PurchaseOrderHistoryReader.StreamType)
                throw new InvalidDataException("The owned stream has an unexpected type.");
            if (stream.Version != expectedVersion)
                return new PurchaseOrderChangeResult.Conflict();
            current =
                await history.ReadAtVersionAsync(id, expectedVersion, cancellationToken)
                ?? throw new InvalidDataException(
                    "The owned stream disappeared during write preparation."
                );
            if (current.RecordedAt != stream.UpdatedAt)
                throw new InvalidDataException(
                    "The observed header and selected history disagree."
                );
            if (recordedAt < stream.UpdatedAt)
                throw new ArgumentOutOfRangeException(
                    nameof(recordedAt),
                    "Recorded time cannot regress."
                );
        }

        long nextVersion = checked(expectedVersion + events.LongLength);
        var proposed = PurchaseOrderEvolution.Apply(current, id, nextVersion, recordedAt, events);
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
                StreamType = PurchaseOrderHistoryReader.StreamType,
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
        return new PurchaseOrderChangeResult.Staged(proposed);
    }
}
