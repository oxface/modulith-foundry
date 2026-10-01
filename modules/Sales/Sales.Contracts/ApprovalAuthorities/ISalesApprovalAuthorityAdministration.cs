using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ISalesApprovalAuthorityAdministration
{
    // All operations require current sales.approval-authorities.manage permission.
    // Set requires an active target tenure; Revoke may clean up an ended tenure.
    Task<SetSalesApprovalAuthorityResult> SetAsync(
        SetSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken = default
    );

    Task<GetSalesApprovalAuthorityResult> GetAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        MembershipId membershipId,
        CancellationToken cancellationToken = default
    );

    Task<RevokeSalesApprovalAuthorityResult> RevokeAsync(
        RevokeSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken = default
    );
}
