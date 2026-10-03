namespace ModulithFoundry.Modules.Inventory.Contracts;

/// <summary>Complete reference state; Revision is Inventory's committed reference-feed position.</summary>
public sealed record StockItemReferenceChangedV1(
    Guid MessageId,
    StockItemReferenceStateV1 Item,
    DateTimeOffset CreatedAt
)
{
    public const string LogicalName = "inventory.stock-item-reference-changed.v1";
}
