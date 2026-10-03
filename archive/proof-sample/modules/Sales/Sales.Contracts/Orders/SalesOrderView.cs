using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public sealed record SalesOrderView(
    SalesOrderId SalesOrderId,
    long OrderNumber,
    CustomerId CustomerId,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<SalesOrderLineView> Lines,
    SalesOrderStatus Status,
    long Version,
    UserId? SubmittedBy,
    DateTimeOffset? SubmittedAt,
    UserId? ApprovedBy,
    DateTimeOffset? ApprovedAt,
    UserId? CancelledBy,
    DateTimeOffset? CancelledAt,
    string? CancellationReason
);
