using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionEvolution
{
    internal static StockPositionState Evolve(
        StockPositionState? state,
        IStockPositionEvent @event
    ) =>
        @event switch
        {
            StockPositionOpened opened when state is null => Open(opened),
            StockReceived received when state is not null => state with
            {
                OnHand = state.OnHand.ApplyRecordedIncrease(received.Quantity),
            },
            StockQuantityCorrected corrected when state is not null => state with
            {
                OnHand = Quantity.Restore(corrected.OnHandQuantity),
            },
            _ => throw new InvalidOperationException(
                $"Event '{@event.GetType().Name}' is invalid for the current Stock Position state."
            ),
        };

    private static StockPositionState Open(StockPositionOpened opened)
    {
        return new(
            opened.StockItemId,
            opened.StockingLocationId,
            opened.BaseUnitCode,
            Quantity.Restore(0m),
            Quantity.Restore(0m)
        );
    }
}
