namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesOrderStatusValues
{
    public const string Draft = "draft";
    public const string AwaitingApproval = "awaiting-approval";
    public const string Approved = "approved";
    public const string Cancelled = "cancelled";

    public static string ToValue(SalesOrderStatus status) =>
        status switch
        {
            SalesOrderStatus.Draft => Draft,
            SalesOrderStatus.AwaitingApproval => AwaitingApproval,
            SalesOrderStatus.Approved => Approved,
            SalesOrderStatus.Cancelled => Cancelled,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown sales order status."
            ),
        };

    public static SalesOrderStatus FromValue(string value) =>
        value switch
        {
            Draft => SalesOrderStatus.Draft,
            AwaitingApproval => SalesOrderStatus.AwaitingApproval,
            Approved => SalesOrderStatus.Approved,
            Cancelled => SalesOrderStatus.Cancelled,
            _ => throw new InvalidOperationException($"Unknown sales order status '{value}'."),
        };
}
