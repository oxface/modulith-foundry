namespace ModulithFoundry.Modules.Sales.Contracts;

public static class OrderFulfilmentLineStatusValues
{
    public const string PendingReservation = "pending-reservation";
    public const string Reserved = "reserved";
    public const string Shortage = "shortage";
    public const string Rejected = "rejected";

    public static string ToValue(OrderFulfilmentLineStatus status) =>
        status switch
        {
            OrderFulfilmentLineStatus.PendingReservation => PendingReservation,
            OrderFulfilmentLineStatus.Reserved => Reserved,
            OrderFulfilmentLineStatus.Shortage => Shortage,
            OrderFulfilmentLineStatus.Rejected => Rejected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown fulfilment line status."
            ),
        };

    public static OrderFulfilmentLineStatus FromValue(string value) =>
        value switch
        {
            PendingReservation => OrderFulfilmentLineStatus.PendingReservation,
            Reserved => OrderFulfilmentLineStatus.Reserved,
            Shortage => OrderFulfilmentLineStatus.Shortage,
            Rejected => OrderFulfilmentLineStatus.Rejected,
            _ => throw new InvalidOperationException($"Unknown fulfilment line status '{value}'."),
        };
}
