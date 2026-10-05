using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory.Contracts;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory;

// Fresh consumer fixtures, not persisted data or a substitute for database tenant filters.
internal sealed class FixtureStockCatalog(ITenantContextAccessor accessor) : IStockCatalog
{
    public StockAvailability Read()
    {
        TenantId tenant = accessor.Current.RequireTenant();
        int quantity = tenant.Value switch
        {
            "wholesale-alpha" => 42,
            "wholesale-beta" => 7,
            _ => throw new InvalidOperationException(
                "No catalog fixture exists for the selected Organization."
            ),
        };
        return new StockAvailability("DEMO-NOTEBOOK", quantity);
    }
}
