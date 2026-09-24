using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Identity;
using ModulithFoundry.Modules.Access.Organizations.CreateOrganization;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Composition;

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(this IServiceCollection services)
    {
        services.AddAccessPersistence();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IExternalIdentityLinker, ExternalIdentityLinker>();
        services.AddScoped<IOrganizationCreation, CreateOrganizationHandler>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        return services;
    }

    public static IServiceCollection AddAccessPersistence(this IServiceCollection services)
    {
        services.AddDbContext<AccessDbContext>((serviceProvider, options) =>
            options.UseNpgsql(serviceProvider.GetRequiredService<NpgsqlDataSource>(), postgres =>
            {
                postgres.MigrationsAssembly(typeof(AccessModule).Assembly.FullName);
                postgres.MigrationsHistoryTable("__EFMigrationsHistory", AccessDbContext.Schema);
            }));

        return services;
    }

    public static async Task MigrateAccessAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<AccessDbContext>();
        await context.Database.MigrateAsync(cancellationToken);
    }
}
