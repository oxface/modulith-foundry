using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionEvolution
{
    internal static StockPositionState Evolve(
        StockPositionState? state,
        IStockPositionEvent @event) =>
        @event switch
        {
            StockPositionOpened opened when state is null => Open(opened),
            StockReceived received when state is not null => state with
            {
                OnHand = state.OnHand.Add(Quantity.Positive(received.Quantity)),
            },
            _ => throw new InvalidOperationException(
                $"Event '{@event.GetType().Name}' is invalid for the current Stock Position state."),
        };

    private static StockPositionState Open(StockPositionOpened opened)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(opened.StockItemId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(opened.StockingLocationId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(opened.BaseUnitCode);
        return new(opened.StockItemId, opened.StockingLocationId, opened.BaseUnitCode,
            Quantity.FromStored(0m), Quantity.FromStored(0m));
    }
}
