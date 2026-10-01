namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record OrderFulfilmentView(
    Guid ProcessId,
    long OrderNumber,
    OrderFulfilmentStatus Status,
    DateTimeOffset CreatedAt,
    long Version,
    Guid? StockingLocationId,
    IReadOnlyList<OrderFulfilmentLineView> Lines
);
