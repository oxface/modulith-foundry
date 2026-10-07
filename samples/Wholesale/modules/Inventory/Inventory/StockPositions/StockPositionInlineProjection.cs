using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionInlineProjection(InventoryDbContext database)
{
    internal Task<StockPositionCurrentRow> LoadAsync(
        EventStream stream,
        CancellationToken cancellationToken
    ) =>
        new InlineProjectionStorage<EventStream, StockPositionCurrentRow>(database).LoadAsync(
            stream,
            cancellationToken
        );
}
