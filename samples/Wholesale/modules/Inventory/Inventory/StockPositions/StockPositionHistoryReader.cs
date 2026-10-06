using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Events.History;
using ModulithFoundry.Events.Serialization;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionHistoryReader(InventoryDbContext database)
    : IStockPositionHistory
{
    internal const string StreamType = "inventory.stock-position";
    private static readonly JsonEventCodec<IStockPositionEvent> Codec =
        StockPositionCodec.CreateCodec();

    public Task<StockPositionHistory?> ReadCurrentAsync(
        Guid id,
        CancellationToken cancellationToken
    ) => ReadAsync(id, null, null, cancellationToken);

    public Task<StockPositionHistory?> ReadAtVersionAsync(
        Guid id,
        long version,
        CancellationToken cancellationToken
    )
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(version, 1);
        return ReadAsync(id, version, null, cancellationToken);
    }

    public Task<StockPositionHistory?> ReadAsOfAsync(
        Guid id,
        DateTimeOffset recordedAt,
        CancellationToken cancellationToken
    ) => ReadAsync(id, null, recordedAt.ToUniversalTime(), cancellationToken);

    private async Task<StockPositionHistory?> ReadAsync(
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
            throw new InvalidDataException("The stock position stream header is invalid.");
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
                throw new InvalidDataException("The stock position creation event is missing.");
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
                "The stock position header and event timestamps disagree."
            );
        return StockPositionEvolution.Rehydrate(
            id,
            target,
            rows[^1].RecordedAt,
            rows.Select(row => Codec.Deserialize(row.EventName, row.SchemaVersion, row.Payload))
        );
    }
}
