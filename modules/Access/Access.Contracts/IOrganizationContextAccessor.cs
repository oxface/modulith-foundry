namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationContextAccessor
{
    OrganizationAccessContext? OrganizationContext { get; }
}
