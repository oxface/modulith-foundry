namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed record StockReservationState(
    Guid ReservationId,
    Guid OperationId,
    decimal Quantity
);
