namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesOrderActivityKindValues
{
    public const string Created = "created";
    public const string Submitted = "submitted";
    public const string Approved = "approved";
    public const string FulfilmentStarted = "fulfilment-started";
    public const string StockReserved = "stock-reserved";
    public const string StockShortage = "stock-shortage";
    public const string ReservationRejected = "reservation-rejected";
    public const string ReplenishmentCreated = "replenishment-created";
    public const string ReplenishmentRejected = "replenishment-rejected";
    public const string Cancelled = "cancelled";
    public const string ReservationReleased = "reservation-released";
    public const string ReservationReleaseRejected = "reservation-release-rejected";
    public const string CompensationCompleted = "compensation-completed";

    public static string ToValue(SalesOrderActivityKind kind) =>
        kind switch
        {
            SalesOrderActivityKind.Created => Created,
            SalesOrderActivityKind.Submitted => Submitted,
            SalesOrderActivityKind.Approved => Approved,
            SalesOrderActivityKind.FulfilmentStarted => FulfilmentStarted,
            SalesOrderActivityKind.StockReserved => StockReserved,
            SalesOrderActivityKind.StockShortage => StockShortage,
            SalesOrderActivityKind.ReservationRejected => ReservationRejected,
            SalesOrderActivityKind.ReplenishmentCreated => ReplenishmentCreated,
            SalesOrderActivityKind.ReplenishmentRejected => ReplenishmentRejected,
            SalesOrderActivityKind.Cancelled => Cancelled,
            SalesOrderActivityKind.ReservationReleased => ReservationReleased,
            SalesOrderActivityKind.ReservationReleaseRejected => ReservationReleaseRejected,
            SalesOrderActivityKind.CompensationCompleted => CompensationCompleted,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown sales order activity kind."
            ),
        };

    public static SalesOrderActivityKind FromValue(string value) =>
        value switch
        {
            Created => SalesOrderActivityKind.Created,
            Submitted => SalesOrderActivityKind.Submitted,
            Approved => SalesOrderActivityKind.Approved,
            FulfilmentStarted => SalesOrderActivityKind.FulfilmentStarted,
            StockReserved => SalesOrderActivityKind.StockReserved,
            StockShortage => SalesOrderActivityKind.StockShortage,
            ReservationRejected => SalesOrderActivityKind.ReservationRejected,
            ReplenishmentCreated => SalesOrderActivityKind.ReplenishmentCreated,
            ReplenishmentRejected => SalesOrderActivityKind.ReplenishmentRejected,
            Cancelled => SalesOrderActivityKind.Cancelled,
            ReservationReleased => SalesOrderActivityKind.ReservationReleased,
            ReservationReleaseRejected => SalesOrderActivityKind.ReservationReleaseRejected,
            CompensationCompleted => SalesOrderActivityKind.CompensationCompleted,
            _ => throw new InvalidOperationException(
                $"Unknown sales order activity kind '{value}'."
            ),
        };
}
