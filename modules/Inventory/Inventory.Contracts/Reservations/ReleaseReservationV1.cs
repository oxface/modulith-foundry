namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record ReleaseReservationV1(
    Guid MessageId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid ReservationOperationId,
    Guid ReservationId,
    Guid StockItemId,
    Guid StockingLocationId,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "inventory.release-reservation.v1";
}
