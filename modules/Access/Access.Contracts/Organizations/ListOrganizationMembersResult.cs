namespace ModulithFoundry.Modules.Access.Contracts;

public abstract record ListOrganizationMembersResult
{
    private ListOrganizationMembersResult()
    {
    }

    public sealed record Listed(OrganizationMembershipAdministration View)
        : ListOrganizationMembersResult;

    public sealed record PermissionDenied : ListOrganizationMembersResult;
}
