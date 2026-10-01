namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockReservationOutcomeV1(
    Guid MessageId,
    Guid CausationId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    StockReservationOutcome Outcome,
    Guid? ReservationId,
    decimal RequestedQuantity,
    decimal AvailableQuantity,
    string BaseUnitCode,
    string? ReasonCode,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "inventory.stock-reservation-outcome.v1";
}
