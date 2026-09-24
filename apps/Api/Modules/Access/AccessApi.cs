using ModulithFoundry.Api.Modules.Access.Organizations;

namespace ModulithFoundry.Api.Modules.Access;

internal static class AccessApi
{
    internal static IEndpointRouteBuilder MapAccessApi(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapOrganizationEndpoints();
        return endpoints;
    }
}
