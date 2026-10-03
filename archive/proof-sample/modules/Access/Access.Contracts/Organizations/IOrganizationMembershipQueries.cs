namespace ModulithFoundry.Modules.Access.Contracts;

public interface IOrganizationMembershipQueries
{
    // Fails closed outside the verified Organization context; exposes no personal data.
    Task<bool> IsActiveAsync(
        OrganizationId organizationId,
        MembershipId membershipId,
        CancellationToken cancellationToken = default
    );

    Task<ListOrganizationMembersResult> ListForAdministrationAsync(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken = default
    );
}
