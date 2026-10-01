namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionPolicy
{
    internal static void Validate(StockPositionState? state)
    {
        if (state is null)
        {
            throw new InvalidStockPositionValueException(
                "quantity",
                "A Stock Position must be opened."
            );
        }

        _ = Quantity.NonNegative(state.OnHand.Value);
        _ = Quantity.NonNegative(state.Reserved.Value);
        if (state.Reserved.Value > state.OnHand.Value)
        {
            throw new InvalidStockPositionValueException(
                "quantity",
                "Reserved quantity cannot exceed on-hand quantity."
            );
        }
    }
}
