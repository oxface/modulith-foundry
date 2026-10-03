namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockItemReferenceResolution(
    IReadOnlyList<StockItemReference> Items,
    IReadOnlyList<StockItemId> MissingItemIds,
    IReadOnlyList<StockItemId> InactiveItemIds
);
