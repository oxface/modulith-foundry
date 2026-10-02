namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;

internal sealed class StockItemReferenceFeed
{
    internal int Id { get; private set; } = 1;
    internal long Revision { get; private set; }

    internal long Advance() => Revision = checked(Revision + 1);
}
