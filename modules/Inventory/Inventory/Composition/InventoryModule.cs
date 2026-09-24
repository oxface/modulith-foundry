using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Inventory.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Inventory.Composition;

public static class InventoryModule
{
    public static IServiceCollection AddInventory(this IServiceCollection services)
    {
        services.AddInventoryPersistence();
        return services;
    }

    public static IServiceCollection AddInventoryPersistence(this IServiceCollection services)
    {
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
