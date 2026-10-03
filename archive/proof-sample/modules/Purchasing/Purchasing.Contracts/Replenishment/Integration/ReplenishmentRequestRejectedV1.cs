namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record ReplenishmentRequestRejectedV1(
    Guid MessageId,
    Guid CausationId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid StockItemId,
    decimal Quantity,
    string BaseUnitCode,
    string ReasonCode,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "purchasing.replenishment-request-rejected.v1";
}
