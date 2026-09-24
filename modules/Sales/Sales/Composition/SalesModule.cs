using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Sales.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Sales.Composition;

public static class SalesModule
{
    public static IServiceCollection AddSales(this IServiceCollection services)
    {
        services.AddSalesPersistence();
        return services;
    }

    public static IServiceCollection AddSalesPersistence(this IServiceCollection services)
    {
        services.AddDbContext<SalesDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), postgres =>
            {
                postgres.MigrationsAssembly(typeof(SalesModule).Assembly.FullName);
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", SalesDbContext.Schema);
            }));

        return services;
    }

    public static async Task MigrateSalesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
