using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;

internal static class StockingLocationMappings
{
    internal static StockingLocationView ToView(this StockingLocation location) =>
        new(new StockingLocationId(location.Id), location.Code, location.Name, location.IsActive);
}
