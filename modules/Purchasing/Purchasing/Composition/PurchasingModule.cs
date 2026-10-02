using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.StockItemProjection;
using Npgsql;

namespace ModulithFoundry.Modules.Purchasing.Composition;

public static class PurchasingModule
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
    {
        services.AddPurchasingPersistence();
        services.AddSingleton(PurchasingAuthorizationManifest.Instance);
        services.AddScoped<IStockItemProjectionBootstrapper, StockItemProjectionBootstrapper>();
        services.AddScoped<IStockItemProjectionQueries, StockItemProjectionQueries>();
        services.AddScoped<IStockItemProjectionReconciliation, StockItemProjectionReconciliation>();
        services.TryAddSingleton<StockItemSubscriptionBarrier>();

        return services;
    }

    public static IServiceCollection AddPurchasingPersistence(this IServiceCollection services)
    {
        services.TryAddScoped<
            IOrganizationContextAccessor,
            UnresolvedOrganizationContextAccessor
        >();
        services.AddDbContext<PurchasingDbContext>(
            (serviceProvider, options) =>
                options.UseNpgsql(
                    serviceProvider.GetRequiredService<NpgsqlDataSource>(),
                    postgres =>
                    {
                        postgres.MigrationsAssembly(typeof(PurchasingModule).Assembly.FullName);
                        postgres.MigrationsHistoryTable(
                            "__EFMigrationsHistory",
                            PurchasingDbContext.Schema
                        );
                    }
                )
        );

        return services;
    }

    public static async Task MigratePurchasingAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default
    )
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PurchasingDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
