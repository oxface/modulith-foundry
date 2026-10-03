using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Api.Modules.Purchasing.Requirements;

namespace ModulithFoundry.Api.Modules.Purchasing;

internal static class PurchasingApi
{
    internal static IEndpointRouteBuilder MapPurchasingApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints
            .MapGroup("/api/o/{organizationSlug}/purchasing")
            .RequireAuthorization()
            .WithMetadata(OrganizationScopeMetadata.Instance)
            .MapReplenishmentRequirementEndpoints();
        return endpoints;
    }
}
