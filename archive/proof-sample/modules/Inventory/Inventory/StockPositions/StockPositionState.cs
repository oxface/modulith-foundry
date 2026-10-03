namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed record StockPositionState(
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode,
    Quantity OnHand,
    Quantity Reserved,
    IReadOnlyList<StockReservationState>? Reservations = null
)
{
    internal Quantity Available => Quantity.Restore(checked(OnHand.Value - Reserved.Value));
}
