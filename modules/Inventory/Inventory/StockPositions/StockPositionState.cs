namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed record StockPositionState(
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode,
    Quantity OnHand,
    Quantity Reserved)
{
    internal Quantity Available => Quantity.FromStored(OnHand.Value - Reserved.Value);
}
