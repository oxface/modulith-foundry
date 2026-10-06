using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Samples.Wholesale.Purchasing.Contracts;
using ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

namespace ModulithFoundry.Samples.Wholesale.Purchasing;

public static class PurchasingRegistration
{
    public static IServiceCollection AddPurchaseOrderHistory(this IServiceCollection services) =>
        services.AddScoped<IPurchaseOrderHistory, PurchaseOrderHistoryReader>();

    public static IServiceCollection AddPurchaseOrderCommands(this IServiceCollection services)
    {
        services.TryAddScoped<PurchaseOrderInlineProjection>();
        return services.AddScoped<IPurchaseOrderCommands, PurchaseOrderCommands>();
    }

    public static IServiceCollection AddPurchaseOrderQueries(this IServiceCollection services)
    {
        services.TryAddScoped<PurchaseOrderInlineProjection>();
        return services.AddScoped<IPurchaseOrderQueries, PurchaseOrderQueries>();
    }
}
