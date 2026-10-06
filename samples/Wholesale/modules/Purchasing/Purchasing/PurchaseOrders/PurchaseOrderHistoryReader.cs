using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Events.History;
using ModulithFoundry.Events.Serialization;
using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderHistoryReader(PurchasingDbContext database)
    : IPurchaseOrderHistory
{
    internal const string StreamType = "purchasing.purchase-order";
    private static readonly JsonEventCodec<IPurchaseOrderEvent> Codec =
        PurchaseOrderCodec.CreateCodec();

    public Task<PurchaseOrderHistory?> ReadCurrentAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => ReadAsync(id, null, null, cancellationToken);

    public Task<PurchaseOrderHistory?> ReadAtVersionAsync(
        Guid id,
        long version,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        return ReadAsync(id, version, null, cancellationToken);
    }

    public Task<PurchaseOrderHistory?> ReadAsOfAsync(
        Guid id,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    ) => ReadAsync(id, null, recordedAt.ToUniversalTime(), cancellationToken);

    private async Task<PurchaseOrderHistory?> ReadAsync(
        Guid id,
        long? version,
        DateTimeOffset? cutoff,
        CancellationToken cancellationToken
    )
    {
        _ = database.RequiredOrganizationKey;
        EventStream? stream = await database
            .EventStreams.AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == id && row.StreamType == StreamType,
                cancellationToken
            );
        if (stream is null)
            return null;
        if (stream.Version < 1 || stream.CreatedAt > stream.UpdatedAt)
            throw new InvalidDataException("The purchase order stream header is invalid.");
        if (version > stream.Version)
            throw new ArgumentOutOfRangeException(
                nameof(version),
                "The requested version exceeds the captured head."
            );

        // Keep this observed head even if another transaction commits before the event query.
        long target = version ?? stream.Version;
        if (cutoff is { } boundary && boundary < stream.UpdatedAt)
        {
            if (boundary < stream.CreatedAt)
                return null;
            target =
                await database
                    .Events.AsNoTracking()
                    .Where(row =>
                        row.StreamId == id
                        && row.StreamVersion <= stream.Version
                        && row.RecordedAt <= boundary
                    )
                    .Select(row => (long?)row.StreamVersion)
                    .MaxAsync(cancellationToken)
                ?? 0;
            if (target == 0)
                throw new InvalidDataException("The purchase order creation event is missing.");
        }
        StoredEvent[] rows = await database
            .Events.AsNoTracking()
            .Where(row => row.StreamId == id && row.StreamVersion <= target)
            .OrderBy(row => row.StreamVersion)
            .ToArrayAsync(cancellationToken);
        EventHistory.ValidateRange(
            rows.Select(row => new HistoryPosition(row.StreamVersion, row.RecordedAt)),
            0,
            target
        );
        if (
            rows[0].RecordedAt != stream.CreatedAt
            || (target == stream.Version && rows[^1].RecordedAt != stream.UpdatedAt)
        )
            throw new InvalidDataException(
                "The purchase order header and event timestamps disagree."
            );
        return PurchaseOrderEvolution.Rehydrate(
            id,
            target,
            rows[^1].RecordedAt,
            rows.Select(row => Codec.Deserialize(row.EventName, row.SchemaVersion, row.Payload))
        );
    }
}
