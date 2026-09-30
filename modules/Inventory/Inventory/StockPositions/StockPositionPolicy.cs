namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionPolicy
{
    internal static void Validate(StockPositionState? state)
    {
        if (state is null || state.Reserved.Value > state.OnHand.Value)
        {
            throw new InvalidStockPositionValueException(
                "quantity", "Reserved quantity cannot exceed on-hand quantity.");
        }
    }
}
