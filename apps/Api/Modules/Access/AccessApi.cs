using ModulithFoundry.Api.Modules.Access.Invitations;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Api.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access;

internal static class AccessApi
{
    internal static IServiceCollection AddAccessApi(this IServiceCollection services)
    {
        services.AddScoped<OrganizationContextAccessor>();
        services.AddScoped<IOrganizationContextAccessor>(serviceProvider =>
            serviceProvider.GetRequiredService<OrganizationContextAccessor>());
        return services;
    }

    internal static IApplicationBuilder UseOrganizationScope(this IApplicationBuilder app) =>
        app.UseMiddleware<OrganizationScopeMiddleware>();

    internal static IEndpointRouteBuilder MapAccessApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationEndpoints();
        RouteGroupBuilder organization = endpoints.MapGroup("/api/o/{organizationSlug}")
            .RequireAuthorization()
            .WithMetadata(OrganizationScopeMetadata.Instance);
        organization.MapOrganizationScopeEndpoints();
        organization.MapInvitationEndpoints();
        return endpoints;
    }
}
