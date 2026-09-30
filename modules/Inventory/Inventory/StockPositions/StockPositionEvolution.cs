using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionEvolution
{
    internal static StockPositionState Evolve(
        StockPositionState? state,
        IStockPositionEvent @event) =>
        @event switch
        {
            StockPositionOpened opened when state is null => new StockPositionState(
                opened.StockItemId,
                opened.StockingLocationId,
                opened.BaseUnitCode,
                Quantity.FromStored(0m),
                Quantity.FromStored(0m)),
            StockReceived received when state is not null => state with
            {
                OnHand = state.OnHand.Add(Quantity.Positive(received.Quantity)),
            },
            _ => throw new InvalidOperationException(
                $"Event '{@event.GetType().Name}' is invalid for the current Stock Position state."),
        };
}
