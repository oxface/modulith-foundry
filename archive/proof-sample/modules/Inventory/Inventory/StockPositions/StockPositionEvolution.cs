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
            StockReserved reserved when state is not null => state with
            {
                Reserved = state.Reserved.ApplyRecordedIncrease(reserved.Quantity),
                Reservations =
                [
                    .. state.Reservations ?? [],
                    new(reserved.ReservationId, reserved.OperationId, reserved.Quantity),
                ],
            },
            StockReservationReleased released when state is not null => Release(state, released),
            _ => throw new InvalidOperationException(
                $"Event '{@event.GetType().Name}' is invalid for the current Stock Position state."
            ),
        };

    private static StockPositionState Release(
        StockPositionState state,
        StockReservationReleased released
    )
    {
        if (
            state.Reservations?.Any(item => item.ReservationId == released.ReservationId)
            is not true
        )
            throw new InvalidOperationException("A release event has no preceding reservation.");
        return state with
        {
            Reserved = state.Reserved.ApplyRecordedIncrease(-released.Quantity),
            Reservations =
            [
                .. state.Reservations.Select(item =>
                    item.ReservationId == released.ReservationId
                        ? item with
                        {
                            IsReleased = true,
                        }
                        : item
                ),
            ],
        };
    }

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
