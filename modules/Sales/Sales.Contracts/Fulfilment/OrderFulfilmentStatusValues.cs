namespace ModulithFoundry.Modules.Sales.Contracts;

public static class OrderFulfilmentStatusValues
{
    public const string PendingDispatch = "pending-dispatch";

    public static string ToValue(OrderFulfilmentStatus status) =>
        status switch
        {
            OrderFulfilmentStatus.PendingDispatch => PendingDispatch,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown fulfilment status."
            ),
        };

    public static OrderFulfilmentStatus FromValue(string value) =>
        value switch
        {
            PendingDispatch => OrderFulfilmentStatus.PendingDispatch,
            _ => throw new InvalidOperationException($"Unknown fulfilment status '{value}'."),
        };
}
