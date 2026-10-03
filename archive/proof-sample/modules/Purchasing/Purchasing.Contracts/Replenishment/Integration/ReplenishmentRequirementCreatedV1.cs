namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record ReplenishmentRequirementCreatedV1(
    Guid MessageId,
    Guid CausationId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid StockItemId,
    Guid RequirementId,
    long RequirementNumber,
    decimal Quantity,
    string BaseUnitCode,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "purchasing.replenishment-requirement-created.v1";
}
