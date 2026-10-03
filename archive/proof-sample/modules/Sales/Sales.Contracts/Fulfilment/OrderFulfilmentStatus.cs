namespace ModulithFoundry.Modules.Sales.Contracts;

public enum OrderFulfilmentStatus
{
    PendingDispatch = 1,
    AwaitingReservations = 2,
    Reserved = 3,
    AwaitingReplenishment = 4,
    AttentionRequired = 5,
    CompensationPending = 6,
    Compensated = 7,
}
