using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Persistence;

internal sealed class UnresolvedOrganizationContextAccessor : IOrganizationContextAccessor
{
    public OrganizationAccessContext? OrganizationContext => null;
}
