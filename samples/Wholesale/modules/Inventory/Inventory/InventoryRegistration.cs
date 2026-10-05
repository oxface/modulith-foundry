using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory;

public static class InventoryRegistration
{
    public static IServiceCollection AddInventoryQueries(this IServiceCollection services) =>
        services.AddScoped<IStockCatalog, StockCatalog>();
}
