namespace ModulithFoundry.Modules.Sales.Contracts;

public static class OrderFulfilmentStatusValues
{
    public const string PendingDispatch = "pending-dispatch";
    public const string AwaitingReservations = "awaiting-reservations";
    public const string Reserved = "reserved";
    public const string AwaitingReplenishment = "awaiting-replenishment";
    public const string AttentionRequired = "attention-required";
    public const string CompensationPending = "compensation-pending";
    public const string Compensated = "compensated";

    public static string ToValue(OrderFulfilmentStatus status) =>
        status switch
        {
            OrderFulfilmentStatus.PendingDispatch => PendingDispatch,
            OrderFulfilmentStatus.AwaitingReservations => AwaitingReservations,
            OrderFulfilmentStatus.Reserved => Reserved,
            OrderFulfilmentStatus.AwaitingReplenishment => AwaitingReplenishment,
            OrderFulfilmentStatus.AttentionRequired => AttentionRequired,
            OrderFulfilmentStatus.CompensationPending => CompensationPending,
            OrderFulfilmentStatus.Compensated => Compensated,
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
            AwaitingReservations => OrderFulfilmentStatus.AwaitingReservations,
            Reserved => OrderFulfilmentStatus.Reserved,
            AwaitingReplenishment => OrderFulfilmentStatus.AwaitingReplenishment,
            AttentionRequired => OrderFulfilmentStatus.AttentionRequired,
            CompensationPending => OrderFulfilmentStatus.CompensationPending,
            Compensated => OrderFulfilmentStatus.Compensated,
            _ => throw new InvalidOperationException($"Unknown fulfilment status '{value}'."),
        };
}
