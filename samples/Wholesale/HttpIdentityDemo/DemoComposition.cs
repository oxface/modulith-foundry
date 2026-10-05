using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using ModulithFoundry.ActorIdentity;
using ModulithFoundry.ActorIdentity.AspNetCore;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Persistence;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory;
using ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Inventory.Contracts;
using ModulithFoundry.Tenancy;
using ModulithFoundry.Tenancy.AspNetCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo;

public static class DemoComposition
{
    public static void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        NativeAuthentication.Configure(services, configuration);
        services.AddAuthorization(options =>
        {
            var authenticated = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.DefaultPolicy = authenticated;
            options.FallbackPolicy = authenticated;
        });
        services.AddApplicationAccess(AccessDatabase.ConnectionString(configuration));
        services.AddScoped<IStockCatalog, FixtureStockCatalog>();
        services.AddProblemDetails();
        services.AddExceptionHandler<ContextExceptionHandler>();
    }

    public static void ConfigureHttp(WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseHttpActorContext();
        app.UseHttpTenantContext();
        app.MapGet("/health", () => TypedResults.Ok("healthy")).AllowAnonymous().AllowTenantless();
        app.MapGet("/identity", ReadIdentity).RequireAuthorization().AllowTenantless();
        app.MapGet("/public-identity", ReadIdentity).AllowAnonymous().AllowTenantless();
        app.MapGet(
                "/login",
                () =>
                    Results.Challenge(
                        new AuthenticationProperties { RedirectUri = "/identity" },
                        [NativeAuthentication.OidcScheme]
                    )
            )
            .AllowAnonymous()
            .AllowTenantless();
    }

    public static void MapOrganizationEndpoints(
        IEndpointRouteBuilder endpoints,
        string catalogPattern,
        string identityPattern
    )
    {
        endpoints
            .MapGet(catalogPattern, ReadCatalog)
            .AllowAnonymous()
            .AllowPublicOrganizationAccess();
        endpoints.MapGet(identityPattern, ReadTenantIdentity).RequireAuthorization();
    }

    private static Ok<IdentityResponse> ReadIdentity(IActorContextAccessor accessor)
    {
        Actor actor = accessor.Current.Actor;
        return TypedResults.Ok(new IdentityResponse(actor.Kind, actor.Id?.Value));
    }

    private static Ok<TenantIdentityResponse> ReadTenantIdentity(
        IActorContextAccessor actor,
        ITenantContextAccessor tenant
    ) => TypedResults.Ok(TenantIdentity(actor, tenant));

    private static Ok<CatalogResponse> ReadCatalog(
        IStockCatalog catalog,
        IActorContextAccessor actor,
        ITenantContextAccessor tenant
    ) => TypedResults.Ok(new CatalogResponse(TenantIdentity(actor, tenant), catalog.Read()));

    private static TenantIdentityResponse TenantIdentity(
        IActorContextAccessor actorAccessor,
        ITenantContextAccessor tenantAccessor
    )
    {
        Actor actor = actorAccessor.Current.Actor;
        return new TenantIdentityResponse(
            actor.Kind,
            actor.Id?.Value,
            tenantAccessor.Current.RequireTenant().Value
        );
    }
}
