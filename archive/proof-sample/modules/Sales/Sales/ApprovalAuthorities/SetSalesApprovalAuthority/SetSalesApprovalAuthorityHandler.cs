using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.ApprovalAuthorities.Persistence;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Persistence;
using Npgsql;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities.SetSalesApprovalAuthority;

internal sealed class SetSalesApprovalAuthorityHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    IOrganizationMembershipQueries memberships,
    TimeProvider timeProvider
)
{
    internal async Task<SetSalesApprovalAuthorityResult> HandleAsync(
        SetSalesApprovalAuthorityCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
            return new SetSalesApprovalAuthorityResult.PermissionDenied();
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
                    SalesAuditActions.ApprovalAuthoritySetDenied,
                    SalesAuditSubjectTypes.ApprovalAuthority,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new SetSalesApprovalAuthorityResult.PermissionDenied();
        }
        ApprovalLimit limit;
        try
        {
            limit = ApprovalLimit.Create(command.MaximumAmount, command.Currency);
        }
        catch (InvalidApprovalLimitException exception)
        {
            return new SetSalesApprovalAuthorityResult.Invalid(exception.Field, exception.Message);
        }
        if (command.ExpectedVersion < 0)
            return new SetSalesApprovalAuthorityResult.Invalid(
                nameof(command.ExpectedVersion),
                "Use zero for creation or the current positive version."
            );
        if (
            !await memberships.IsActiveAsync(
                command.OrganizationId,
                command.MembershipId,
                cancellationToken
            )
        )
            return new SetSalesApprovalAuthorityResult.MembershipUnavailable();
        SalesApprovalAuthority? authority = await context.ApprovalAuthorities.SingleOrDefaultAsync(
            entity =>
                entity.OrganizationId == command.OrganizationId.Value
                && entity.MembershipId == command.MembershipId.Value,
            cancellationToken
        );
        if ((authority?.Version ?? 0) != command.ExpectedVersion)
            return new SetSalesApprovalAuthorityResult.VersionConflict();
        if (authority is null)
        {
            authority = SalesApprovalAuthority.Create(
                command.OrganizationId.Value,
                command.MembershipId.Value,
                limit
            );
            context.ApprovalAuthorities.Add(authority);
        }
        else
            authority.Set(limit);
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.ApprovalAuthoritySet,
                SalesAuditSubjectTypes.ApprovalAuthority,
                authority.Id,
                new
                {
                    authority.MembershipId,
                    authority.MaximumAmount,
                    authority.Currency,
                    authority.Version,
                },
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
            return new SetSalesApprovalAuthorityResult.VersionConflict();
        }
        catch (DbUpdateException exception)
            when (exception.InnerException
                    is PostgresException
                    {
                        SqlState: PostgresErrorCodes.UniqueViolation,
                        ConstraintName: SalesApprovalAuthorityConfiguration.MembershipConstraint
                    }
            )
        {
            context.ChangeTracker.Clear();
            return new SetSalesApprovalAuthorityResult.VersionConflict();
        }
        return new SetSalesApprovalAuthorityResult.Saved(authority.ToView());
    }
}
