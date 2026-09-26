using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class UnresolvedOrganizationContextAccessor : IOrganizationContextAccessor
{
    public OrganizationAccessContext? OrganizationContext => null;
}
