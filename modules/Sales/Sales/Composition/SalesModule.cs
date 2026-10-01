using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Customers;
using ModulithFoundry.Modules.Sales.Customers.CreateCustomer;
using ModulithFoundry.Modules.Sales.Customers.Queries;
using ModulithFoundry.Modules.Sales.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Sales.Composition;

public static class SalesModule
{
    public static IServiceCollection AddSalesModule(this IServiceCollection services)
    {
        services.AddSalesPersistence();
        services.AddSingleton(SalesAuthorizationManifest.Instance);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<SalesRequestAuthorization>();
        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<CustomerQueries>();
        services.AddScoped<ICustomerAdministration, CustomerAdministration>();

        return services;
    }

    public static IServiceCollection AddSalesPersistence(this IServiceCollection services)
    {
        services.TryAddScoped<IOrganizationContextAccessor, UnresolvedOrganizationContextAccessor>();
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
