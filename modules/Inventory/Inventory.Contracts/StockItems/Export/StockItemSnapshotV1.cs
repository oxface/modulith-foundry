namespace ModulithFoundry.Modules.Inventory.Contracts;

/// <summary>Complete current references and a committed-prefix boundary, not an event replay cursor.</summary>
public sealed record StockItemSnapshotV1(
    long HighWatermark,
    IReadOnlyList<StockItemReferenceStateV1> Items
);
