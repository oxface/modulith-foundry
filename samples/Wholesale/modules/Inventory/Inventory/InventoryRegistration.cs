using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

namespace ModulithFoundry.Samples.Wholesale.Inventory;

public static class InventoryRegistration
{
    public static IServiceCollection AddInventoryQueries(this IServiceCollection services) =>
        services.AddScoped<IStockCatalog, StockCatalog>();

    public static IServiceCollection AddStockPositionHistory(this IServiceCollection services) =>
        services.AddScoped<IStockPositionHistory, StockPositionHistoryReader>();

    public static IServiceCollection AddStockPositionCommands(this IServiceCollection services) =>
        services.AddScoped<IStockPositionCommands, StockPositionCommands>();
}
