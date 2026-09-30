using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.CreateStockingLocation;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.Queries;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.RenameStockingLocation;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations.SetStockingLocationActive;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.ChangeStockItemDescription;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.CreateStockItem;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Queries;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.SetStockItemActive;
using Npgsql;

namespace ModulithFoundry.Modules.Inventory.Composition;

public static class InventoryModule
{
    public static IServiceCollection AddInventoryModule(this IServiceCollection services)
    {
        services.AddInventoryPersistence();
        services.AddSingleton(InventoryAuthorizationManifest.Instance);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<InventoryRequestAuthorization>();
        services.AddScoped<CreateStockItemHandler>();
        services.AddScoped<ChangeStockItemDescriptionHandler>();
        services.AddScoped<SetStockItemActiveHandler>();
        services.AddScoped<StockItemQueries>();
        services.AddScoped<CreateStockingLocationHandler>();
        services.AddScoped<RenameStockingLocationHandler>();
        services.AddScoped<SetStockingLocationActiveHandler>();
        services.AddScoped<StockingLocationQueries>();
        services.AddScoped<IStockItemAdministration, StockItemAdministration>();
        services.AddScoped<IStockingLocationAdministration, StockingLocationAdministration>();
        services.AddScoped<IStockItemReferences, StockItemReferences>();

        return services;
    }

    public static IServiceCollection AddInventoryPersistence(this IServiceCollection services)
    {
        services.TryAddScoped<IOrganizationContextAccessor, UnresolvedOrganizationContextAccessor>();
        services.AddDbContext<InventoryDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), postgres =>
            {
                postgres.MigrationsAssembly(typeof(InventoryModule).Assembly.FullName);
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", InventoryDbContext.Schema);
            }));

        return services;
    }

    public static async Task MigrateInventoryAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
