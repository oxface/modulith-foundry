using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities.RevokeSalesApprovalAuthority;

internal sealed class RevokeSalesApprovalAuthorityHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    TimeProvider timeProvider
)
{
    internal async Task<RevokeSalesApprovalAuthorityResult> HandleAsync(
        RevokeSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
            return new RevokeSalesApprovalAuthorityResult.PermissionDenied();
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                SalesPermissionIds.ApprovalAuthoritiesManage,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.ApprovalAuthorityRevokeDenied,
                    SalesAuditSubjectTypes.ApprovalAuthority,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new RevokeSalesApprovalAuthorityResult.PermissionDenied();
        }
        if (command.ExpectedVersion <= 0)
            return new RevokeSalesApprovalAuthorityResult.InvalidExpectedVersion();
        SalesApprovalAuthority? authority = await context.ApprovalAuthorities.SingleOrDefaultAsync(
            entity =>
                entity.OrganizationId == command.OrganizationId.Value
                && entity.MembershipId == command.MembershipId.Value,
            cancellationToken
        );
        if (authority is null)
            return new RevokeSalesApprovalAuthorityResult.NotFound();
        if (authority.Version != command.ExpectedVersion)
            return new RevokeSalesApprovalAuthorityResult.VersionConflict();
        if (!authority.TryRevoke())
            return new RevokeSalesApprovalAuthorityResult.Unchanged(authority.ToView());
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.ApprovalAuthorityRevoked,
                SalesAuditSubjectTypes.ApprovalAuthority,
                authority.Id,
                new { authority.MembershipId, authority.Version },
                timeProvider.GetUtcNow()
            )
        );
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            return new RevokeSalesApprovalAuthorityResult.VersionConflict();
        }
        return new RevokeSalesApprovalAuthorityResult.Revoked(authority.ToView());
    }
}
