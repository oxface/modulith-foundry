namespace ModulithFoundry.Modules.Purchasing.Contracts;

// Trusted Sales workflow intent. This is not accepted through HTTP.
public sealed record CreateReplenishmentRequirementV1(
    Guid MessageId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid StockItemId,
    decimal Quantity,
    string BaseUnitCode,
    DateTimeOffset CreatedAt,
    long MinimumReferenceRevision = 0
)
{
    public const string LogicalName = "purchasing.create-replenishment-requirement.v1";
}
