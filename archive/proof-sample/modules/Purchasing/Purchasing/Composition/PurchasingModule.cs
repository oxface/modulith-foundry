using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.Messaging;
using ModulithFoundry.Modules.Purchasing.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.CreatePurchaseOrder;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.IssuePurchaseOrder;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Queries;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.SetPurchaseOrderLine;
using ModulithFoundry.Modules.Purchasing.Replenishment.Queries;
using ModulithFoundry.Modules.Purchasing.Replenishment.Requests;
using ModulithFoundry.Modules.Purchasing.StockItemProjection;
using Npgsql;

namespace ModulithFoundry.Modules.Purchasing.Composition;

public static class PurchasingModule
{
    public static IServiceCollection AddPurchasingModule(this IServiceCollection services)
    {
        services.AddPurchasingPersistence();
        PurchaseOrderEventSerializer.ValidateRegistry();
        services.AddScoped<PurchaseOrderAuthorization>();
        services.AddScoped<PurchaseOrderStore>();
        services.AddScoped<PurchaseOrderEventReader>();
        services.AddScoped<PurchaseOrderInlineProjection>();
        services.AddScoped<CreatePurchaseOrderHandler>();
        services.AddScoped<SetPurchaseOrderLineHandler>();
        services.AddScoped<IPurchaseOrderIssuance, IssuePurchaseOrderHandler>();
        services.AddScoped<IPurchaseOrderDrafting, PurchaseOrderDrafting>();
        services.AddScoped<IPurchaseOrderQueries, PurchaseOrderQueries>();
        services.TryAddSingleton<PurchasingMessagingMetrics>();
        services.AddSingleton(PurchasingAuthorizationManifest.Instance);
        services.AddScoped<IStockItemProjectionBootstrapper, StockItemProjectionBootstrapper>();
        services.AddScoped<IStockItemProjectionQueries, StockItemProjectionQueries>();
        services.AddScoped<IStockItemProjectionReconciliation, StockItemProjectionReconciliation>();
        services.TryAddSingleton<StockItemSubscriptionBarrier>();
        services.AddScoped<IReplenishmentRequestQueries, ReplenishmentRequestQueries>();
        services.AddScoped<IReplenishmentRequirementQueries, ReplenishmentRequirementQueries>();
        services.AddScoped<ReplenishmentRequestProcessor>();
        services.AddScoped<ReceiveReplenishmentRequestHandler>();
        services.AddScoped<ResumeReplenishmentRequestHandler>();
        services.TryAddSingleton(TimeProvider.System);

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
