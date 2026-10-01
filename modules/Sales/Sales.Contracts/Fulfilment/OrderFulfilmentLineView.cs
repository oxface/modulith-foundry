using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record OrderFulfilmentLineView(
    int LineNumber,
    StockItemId StockItemId,
    decimal Quantity,
    string BaseUnitCode,
    OrderFulfilmentLineStatus Status,
    Guid? ReservationId,
    decimal? AvailableQuantity,
    string? ReasonCode,
    int AttemptCount,
    DateTimeOffset? ResponseDeadline
);
