using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Persistence;

internal sealed class UnresolvedOrganizationContextAccessor : IOrganizationContextAccessor
{
    public OrganizationAccessContext? OrganizationContext => null;
}
