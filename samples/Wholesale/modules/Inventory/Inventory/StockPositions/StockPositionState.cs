using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed record StockPositionState(
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode,
    decimal OnHand,
    string? LatestDeliveryReference
)
{
    internal StockPositionHistory ToHistory(Guid id, long version, DateTimeOffset recordedAt) =>
        new(
            id,
            version,
            recordedAt,
            StockItemId,
            StockingLocationId,
            BaseUnitCode,
            OnHand,
            LatestDeliveryReference
        );
}
