using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

namespace ModulithFoundry.Samples.Wholesale.Inventory;

public static class InventoryRegistration
{
    public static IServiceCollection AddInventoryQueries(this IServiceCollection services) =>
        services.AddScoped<IStockCatalog, StockCatalog>();

    public static IServiceCollection AddStockPositionHistory(this IServiceCollection services) =>
        services.AddScoped<IStockPositionHistory, StockPositionHistoryReader>();

    public static IServiceCollection AddStockPositionCommands(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<IEventStore<StockPositionAggregate>, StockPositionStore>();
        services.TryAddScoped<StockPositionInlineProjection>();
        return services.AddScoped<IStockPositionCommands, StockPositionCommands>();
    }

    public static IServiceCollection AddStockPositionQueries(this IServiceCollection services)
    {
        services.TryAddScoped<StockPositionInlineProjection>();
        return services.AddScoped<IStockPositionQueries, StockPositionQueries>();
    }
}
