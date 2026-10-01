using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

internal static class StockItemMappings
{
    internal static StockItemView ToView(this StockItem item) =>
        new(new StockItemId(item.Id), item.Sku, item.Description, item.BaseUnitCode, item.IsActive);
}
