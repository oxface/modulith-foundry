namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationMembershipQueries
{
    Task<ListOrganizationMembersResult> ListForAdministrationAsync(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken = default
    );
}
