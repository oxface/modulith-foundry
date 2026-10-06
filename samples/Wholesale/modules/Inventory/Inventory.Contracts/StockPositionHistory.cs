namespace ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

public sealed record StockPositionHistory(
    Guid Id,
    long Version,
    DateTimeOffset RecordedAt,
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode,
    decimal OnHand,
    string? LatestDeliveryReference
);
