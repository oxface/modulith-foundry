using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Inventory;
using ModulithFoundry.Samples.Wholesale.Purchasing;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.EventPersistenceDemo;

public static class DemoComposition
{
    public static ServiceCollection CreateServices(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        var services = new ServiceCollection();
        services.AddScoped<TenantContextAccessor>();
        services.AddScoped<ITenantContextAccessor>(provider =>
            provider.GetRequiredService<TenantContextAccessor>()
        );
        services.AddScoped<ITenantContextInitializer>(provider =>
            provider.GetRequiredService<TenantContextAccessor>()
        );
        services.AddDbContext<InventoryDbContext>(options =>
            InventoryDatabase.Configure(options, connectionString)
        );
        services.AddDbContext<PurchasingDbContext>(options =>
            PurchasingDatabase.Configure(options, connectionString)
        );
        services.AddStockPositionHistory();
        services.AddPurchaseOrderHistory();
        return services;
    }
}
