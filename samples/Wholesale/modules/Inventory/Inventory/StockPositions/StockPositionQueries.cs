using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionQueries(
    InventoryDbContext database,
    StockPositionInlineProjection projection
) : IStockPositionQueries
{
    public async Task<StockPositionHistory?> ReadCurrentAsync(
        Guid id,
        CancellationToken cancellationToken
    )
    {
        _ = database.RequiredOrganizationKey;
        var stream = await database
            .EventStreams.AsNoTracking()
            .SingleOrDefaultAsync(
                row => row.Id == id && row.StreamType == StockPositionHistoryReader.StreamType,
                cancellationToken
            );
        if (stream is null)
            return null;
        var current = await projection.LoadAsync(stream, cancellationToken);
        return current.ReadState().ToHistory(id, current.Version, current.RecordedAt);
    }
}
