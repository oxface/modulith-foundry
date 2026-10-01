namespace ModulithFoundry.Modules.Inventory.Contracts;

// Workflow-only directed intent. No HTTP endpoint accepts this contract.
public sealed record ReserveStockV1(
    Guid MessageId,
    Guid OrganizationId,
    Guid OperationId,
    Guid ProcessId,
    long OrderNumber,
    int LineNumber,
    Guid StockItemId,
    Guid StockingLocationId,
    decimal Quantity,
    string BaseUnitCode,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "inventory.reserve-stock.v1";
}
