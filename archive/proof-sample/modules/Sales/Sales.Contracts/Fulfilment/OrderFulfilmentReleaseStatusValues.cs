namespace ModulithFoundry.Modules.Sales.Contracts;

public static class OrderFulfilmentReleaseStatusValues
{
    public const string Pending = "pending";
    public const string Released = "released";
    public const string AlreadyReleased = "already-released";
    public const string Rejected = "rejected";

    public static string ToValue(OrderFulfilmentReleaseStatus status) =>
        status switch
        {
            OrderFulfilmentReleaseStatus.Pending => Pending,
            OrderFulfilmentReleaseStatus.Released => Released,
            OrderFulfilmentReleaseStatus.AlreadyReleased => AlreadyReleased,
            OrderFulfilmentReleaseStatus.Rejected => Rejected,
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown release status."
            ),
        };

    public static OrderFulfilmentReleaseStatus FromValue(string value) =>
        value switch
        {
            Pending => OrderFulfilmentReleaseStatus.Pending,
            Released => OrderFulfilmentReleaseStatus.Released,
            AlreadyReleased => OrderFulfilmentReleaseStatus.AlreadyReleased,
            Rejected => OrderFulfilmentReleaseStatus.Rejected,
            _ => throw new InvalidOperationException($"Unknown release status '{value}'."),
        };
}
