using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities.Queries;

internal sealed class SalesApprovalAuthorityQueries(
    SalesDbContext context,
    SalesRequestAuthorization authorization
)
{
    internal async Task<GetSalesApprovalAuthorityResult> GetAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        MembershipId membershipId,
        CancellationToken cancellationToken
    )
    {
        if (
            !await authorization.HasPermissionAsync(
                actorUserId,
                organizationId,
                SalesPermissionIds.ApprovalAuthoritiesManage,
                cancellationToken
            )
        )
            return new GetSalesApprovalAuthorityResult.PermissionDenied();
        SalesApprovalAuthorityView? authority = await context
            .ApprovalAuthorities.AsNoTracking()
            .Where(entity =>
                entity.OrganizationId == organizationId.Value
                && entity.MembershipId == membershipId.Value
            )
            .Select(entity => new SalesApprovalAuthorityView(
                new MembershipId(entity.MembershipId),
                entity.MaximumAmount,
                entity.Currency,
                entity.IsEnabled,
                entity.Version
            ))
            .SingleOrDefaultAsync(cancellationToken);
        return authority is null
            ? new GetSalesApprovalAuthorityResult.NotFound()
            : new GetSalesApprovalAuthorityResult.Found(authority);
    }
}
