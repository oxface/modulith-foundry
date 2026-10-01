using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionMappings
{
    internal static StockPositionView ToView(
        this StockPositionAggregate aggregate,
        StockItem item,
        StockingLocation location
    )
    {
        StockPositionState state =
            aggregate.State
            ?? throw new InvalidOperationException("Cannot map an empty Stock Position.");
        return new StockPositionView(
            new StockPositionId(aggregate.StreamId),
            new StockItemId(state.StockItemId),
            new StockingLocationId(state.StockingLocationId),
            item.Sku,
            location.Code,
            state.BaseUnitCode,
            state.OnHand.Value,
            state.Reserved.Value,
            state.Available.Value,
            aggregate.Version
        );
    }
}
