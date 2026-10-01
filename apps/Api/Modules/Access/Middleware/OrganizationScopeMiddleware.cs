using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Middleware;

internal sealed class OrganizationScopeMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext httpContext,
        OrganizationContextAccessor contextAccessor,
        IOrganizationQueries organizationQueries
    )
    {
        Endpoint? endpoint = httpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<OrganizationScopeMetadata>() is null)
        {
            await next(httpContext);
            return;
        }

        string? organizationSlug = httpContext.Request.RouteValues["organizationSlug"] as string;
        if (string.IsNullOrWhiteSpace(organizationSlug))
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        CurrentUser currentUser = httpContext.User.GetRequiredCurrentUser();
        OrganizationAccessContext? organizationContext =
            await organizationQueries.ResolveAccessAsync(
                currentUser.UserId,
                organizationSlug,
                httpContext.RequestAborted
            );
        if (organizationContext is null)
        {
            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        contextAccessor.Set(organizationContext);
        await next(httpContext);
    }
}
