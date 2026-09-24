using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Composition;

public static class AccessModule
{
    public static IServiceCollection AddAccess(this IServiceCollection services)
    {
        services.AddAccessPersistence();
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
