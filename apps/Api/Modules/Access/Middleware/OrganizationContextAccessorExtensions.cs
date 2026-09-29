using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Middleware;

internal static class OrganizationContextAccessorExtensions
{
    internal static OrganizationAccessContext GetRequiredOrganizationContext(
        this IOrganizationContextAccessor contextAccessor)
    {
        ArgumentNullException.ThrowIfNull(contextAccessor);

        return contextAccessor.OrganizationContext
            ?? throw new InvalidOperationException("Organization context is not resolved.");
    }
}
