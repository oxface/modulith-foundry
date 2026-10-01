using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Persistence;

internal sealed class UnresolvedOrganizationContextAccessor : IOrganizationContextAccessor
{
    public OrganizationAccessContext? OrganizationContext => null;
}
