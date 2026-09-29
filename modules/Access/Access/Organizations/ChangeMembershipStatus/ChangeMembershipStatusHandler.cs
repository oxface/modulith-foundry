using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.ChangeMembershipStatus;

internal sealed class ChangeMembershipStatusHandler(
    AccessDbContext context,
    MembershipAdministrationConsistency consistency,
    OrganizationMembershipQueries membershipQueries,
    TimeProvider timeProvider)
{
    internal async Task<ChangeMembershipStatusResult> HandleAsync(
        ChangeMembershipStatusCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        bool organizationExists = await consistency.LockOrganizationAsync(
            command.OrganizationId.Value,
            cancellationToken);
        if (!organizationExists)
        {
            return new ChangeMembershipStatusResult.NotFound();
        }

        if (!await membershipQueries.HasPermissionAsync(
                command.ActorUserId.Value,
                command.OrganizationId.Value,
                AccessPermissionIds.MembersManage,
                cancellationToken))
        {
            await RecordDenialAsync(
                command,
                AccessAuditReasonCodes.PermissionDenied,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ChangeMembershipStatusResult.PermissionDenied();
        }

        Membership? membership = await context.Memberships
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Include(item => item.RoleAssignments)
            .SingleOrDefaultAsync(
                item => item.Id == command.MembershipId.Value
                    && item.OrganizationId == command.OrganizationId.Value,
                cancellationToken);
        if (membership is null)
        {
            return new ChangeMembershipStatusResult.NotFound();
        }

        if (membership.WouldDeactivateAdministrator(command.Status)
            && !await consistency.HasAnotherActiveAdministratorAsync(
                command.OrganizationId.Value,
                membership.Id,
                cancellationToken))
        {
            await RecordDenialAsync(
                command,
                AccessAuditReasonCodes.LastAdministrator,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ChangeMembershipStatusResult.LastAdministrator();
        }

        MembershipStatus previousStatus = membership.Status;
        MembershipStatusChangeOutcome outcome = membership.ChangeStatus(command.Status);
        if (outcome == MembershipStatusChangeOutcome.Unchanged)
        {
            await transaction.CommitAsync(cancellationToken);
            return new ChangeMembershipStatusResult.Unchanged(
                command.MembershipId,
                membership.Status);
        }

        if (outcome == MembershipStatusChangeOutcome.InvalidTransition)
        {
            return new ChangeMembershipStatusResult.InvalidTransition(
                previousStatus,
                command.Status);
        }

        DateTimeOffset changedAt = timeProvider.GetUtcNow();
        context.AuditEntries.Add(MembershipAuditEntries.StatusChanged(
            membership,
            command.ActorUserId.Value,
            previousStatus,
            changedAt));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ChangeMembershipStatusResult.Changed(
            command.MembershipId,
            membership.Status);
    }

    private async Task RecordDenialAsync(
        ChangeMembershipStatusCommand command,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        context.AuditEntries.Add(MembershipAuditEntries.StatusChangeDenied(
            command.OrganizationId.Value,
            command.MembershipId.Value,
            command.ActorUserId.Value,
            command.Status,
            reasonCode,
            timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);
    }
}
