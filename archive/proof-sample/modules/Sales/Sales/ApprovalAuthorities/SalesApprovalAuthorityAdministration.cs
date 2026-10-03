using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities.Queries;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities.RevokeSalesApprovalAuthority;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities.SetSalesApprovalAuthority;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities;

internal sealed class SalesApprovalAuthorityAdministration(
    SetSalesApprovalAuthorityHandler set,
    RevokeSalesApprovalAuthorityHandler revoke,
    SalesApprovalAuthorityQueries queries
) : ISalesApprovalAuthorityAdministration
{
    public Task<SetSalesApprovalAuthorityResult> SetAsync(
        SetSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken = default
    ) => set.HandleAsync(command, cancellationToken);

    public Task<GetSalesApprovalAuthorityResult> GetAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        MembershipId membershipId,
        CancellationToken cancellationToken = default
    ) => queries.GetAsync(actorUserId, organizationId, membershipId, cancellationToken);

    public Task<RevokeSalesApprovalAuthorityResult> RevokeAsync(
        RevokeSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken = default
    ) => revoke.HandleAsync(command, cancellationToken);
}
