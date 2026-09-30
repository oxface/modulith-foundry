using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionDecider
{
    internal static IReadOnlyList<IStockPositionEvent> DecideReceipt(
        StockPositionState? state,
        Guid stockItemId,
        Guid stockingLocationId,
        string baseUnitCode,
        Quantity quantity)
    {
        if (state is null)
        {
            return
            [
                new StockPositionOpened(stockItemId, stockingLocationId, baseUnitCode),
                new StockReceived(quantity.Value),
            ];
        }

        if (state.StockItemId != stockItemId
            || state.StockingLocationId != stockingLocationId
            || !string.Equals(state.BaseUnitCode, baseUnitCode, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Stock Position identity does not match its reference data.");
        }

        _ = state.OnHand.Add(quantity);
        return [new StockReceived(quantity.Value)];
    }
}
