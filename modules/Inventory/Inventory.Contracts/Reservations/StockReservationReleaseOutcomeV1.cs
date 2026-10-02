namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockReservationReleaseOutcomeV1(
    Guid MessageId,
    Guid CausationId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid ReservationOperationId,
    Guid ReservationId,
    StockReservationReleaseOutcome Outcome,
    decimal? ReservationQuantity,
    string? BaseUnitCode,
    string? ReasonCode,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "inventory.reservation-release-outcome.v1";
}
