namespace ModulithFoundry.Modules.Sales.Contracts;

public enum SalesOrderActivityKind
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    FulfilmentStarted = 4,
    StockReserved = 5,
    StockShortage = 6,
    ReservationRejected = 7,
    ReplenishmentCreated = 8,
    ReplenishmentRejected = 9,
}
