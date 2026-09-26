using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Api.Modules.Access.Organizations;

internal sealed class OrganizationContextAccessor : IOrganizationContextAccessor
{
    public OrganizationAccessContext? OrganizationContext { get; private set; }

    internal void Set(OrganizationAccessContext organizationContext)
    {
        ArgumentNullException.ThrowIfNull(organizationContext);
        if (OrganizationContext is not null)
        {
            throw new InvalidOperationException("Organization context is already resolved.");
        }

        OrganizationContext = organizationContext;
    }
}
