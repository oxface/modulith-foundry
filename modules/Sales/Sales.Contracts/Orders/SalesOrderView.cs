namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record SalesOrderView(
    SalesOrderId SalesOrderId,
    long OrderNumber,
    CustomerId CustomerId,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<SalesOrderLineView> Lines
);
