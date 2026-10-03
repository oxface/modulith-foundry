using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.ExecutionIdentity;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;

namespace ModulithFoundry.Samples.Wholesale.ContextDemo;

public static class DemoComposition
{
    public static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<OperationContextAccessor>();
        services.AddScoped<IOperationContextAccessor>(provider =>
            provider.GetRequiredService<OperationContextAccessor>()
        );
        services.AddScoped<IOperationContextInitializer>(provider =>
            provider.GetRequiredService<OperationContextAccessor>()
        );
        services.AddSingleton<StockFixture>();
        services.AddScoped<IStockAvailability, StockAvailability>();
        services.AddScoped<IDraftOrderPreview, DraftOrderPreview>();
        return services;
    }
}
