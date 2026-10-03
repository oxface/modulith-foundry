using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal static class StockPositionDecider
{
    internal static IReadOnlyList<IStockPositionEvent> DecideRelease(
        StockPositionState state,
        Guid reservationId,
        Guid operationId
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, Guid.Empty);
        var reservation =
            state.Reservations?.SingleOrDefault(item => item.ReservationId == reservationId)
            ?? throw new InvalidOperationException("Cannot release an unknown reservation.");
        return reservation.IsReleased
            ? []
            : [new StockReservationReleased(reservationId, operationId, reservation.Quantity)];
    }

    internal static IReadOnlyList<IStockPositionEvent> DecideReservation(
        StockPositionState? state,
        Guid reservationId,
        Guid operationId,
        Quantity quantity
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(reservationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, Guid.Empty);
        if (state is null || state.Available.Value < quantity.Value)
            return [];
        if (state.Reservations?.Any(reservation => reservation.OperationId == operationId) is true)
            throw new InvalidOperationException("Reservation operation has already been applied.");
        return [new StockReserved(reservationId, operationId, quantity.Value)];
    }

    internal static IReadOnlyList<IStockPositionEvent> DecideCorrection(
        StockPositionState state,
        Quantity onHand,
        StockCorrectionReason reason
    ) => state.OnHand == onHand ? [] : [new StockQuantityCorrected(onHand.Value, reason.Value)];

    internal static IReadOnlyList<IStockPositionEvent> DecideReceipt(
        StockPositionState? state,
        Guid stockItemId,
        Guid stockingLocationId,
        string baseUnitCode,
        Quantity quantity
    )
    {
        if (state is null)
        {
            ArgumentOutOfRangeException.ThrowIfEqual(stockItemId, Guid.Empty);
            ArgumentOutOfRangeException.ThrowIfEqual(stockingLocationId, Guid.Empty);
            ArgumentException.ThrowIfNullOrWhiteSpace(baseUnitCode);
            return
            [
                new StockPositionOpened(stockItemId, stockingLocationId, baseUnitCode),
                new StockReceived(quantity.Value),
            ];
        }

        if (
            state.StockItemId != stockItemId
            || state.StockingLocationId != stockingLocationId
            || !string.Equals(state.BaseUnitCode, baseUnitCode, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                "Stock Position identity does not match its reference data."
            );
        }

        _ = state.OnHand.Add(quantity);
        return [new StockReceived(quantity.Value)];
    }
}
