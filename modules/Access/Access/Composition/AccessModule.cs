using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Identity;
using ModulithFoundry.Modules.Access.Invitations;
using ModulithFoundry.Modules.Access.Invitations.AcceptInvitation;
using ModulithFoundry.Modules.Access.Invitations.CreateInvitation;
using ModulithFoundry.Modules.Access.Invitations.ResendInvitation;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Organizations.CreateOrganization;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Organizations.ReplaceMembershipRoles;
using ModulithFoundry.Modules.Access.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Access.Composition;

public static class AccessModule
{
    public static IServiceCollection AddAccessModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAccessPersistence();
        services.AddInvitationEmailDelivery(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(serviceProvider =>
            new SystemRoleCatalog(serviceProvider.GetServices<SystemRoleManifest>()));
        services.AddScoped<IExternalIdentityLinking, LinkExternalIdentityHandler>();
        services.AddScoped<IOrganizationCreation, CreateOrganizationHandler>();
        services.AddScoped<OrganizationMembershipQueries>();
        services.AddScoped<IOrganizationMembershipQueries>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizationMembershipQueries>());
        services.AddScoped<IOrganizationMembershipAdministration, ReplaceMembershipRolesHandler>();
        services.AddScoped<InvitationQueries>();
        services.AddScoped<InvitationEmailDeliveryFactory>();
        services.AddScoped<CreateInvitationHandler>();
        services.AddScoped<ResendInvitationHandler>();
        services.AddScoped<AcceptInvitationHandler>();
        services.AddScoped<IOrganizationInvitations, OrganizationInvitations>();
        services.AddScoped<IOrganizationQueries, OrganizationQueries>();
        return services;
    }

    public static IServiceCollection AddAccessPersistence(this IServiceCollection services)
    {
        services.TryAddScoped<IOrganizationContextAccessor, UnresolvedOrganizationContextAccessor>();
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
