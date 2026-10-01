using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.ReplaceMembershipRoles;

internal sealed class ReplaceMembershipRolesHandler(
    AccessDbContext context,
    MembershipAdministrationConsistency consistency,
    OrganizationMembershipQueries membershipQueries,
    SystemRoleCatalog roleCatalog,
    TimeProvider timeProvider
)
{
    internal async Task<ReplaceMembershipRolesResult> HandleAsync(
        ReplaceMembershipRolesCommand command,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.RoleIds);

        string[] roleIds =
        [
            .. command
                .RoleIds.Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken
        );
        bool organizationExists = await consistency.LockOrganizationAsync(
            command.OrganizationId.Value,
            cancellationToken
        );
        if (!organizationExists)
        {
            return new ReplaceMembershipRolesResult.NotFound();
        }

        if (
            !await membershipQueries.HasPermissionAsync(
                command.ActorUserId.Value,
                command.OrganizationId.Value,
                AccessPermissionIds.MembersManage,
                cancellationToken
            )
        )
        {
            await RecordDenialAsync(
                command,
                roleIds,
                AccessAuditReasonCodes.PermissionDenied,
                cancellationToken
            );
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.PermissionDenied();
        }

        string[] invalidRoleIds = [.. roleIds.Where(roleId => !roleCatalog.Contains(roleId))];
        if (roleIds.Length == 0 || invalidRoleIds.Length > 0)
        {
            return new ReplaceMembershipRolesResult.InvalidRoles(invalidRoleIds);
        }

        Membership? membership = await context
            .Memberships.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Include(item => item.RoleAssignments)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == command.MembershipId.Value
                    && item.OrganizationId == command.OrganizationId.Value,
                cancellationToken
            );
        if (membership is null)
        {
            return new ReplaceMembershipRolesResult.NotFound();
        }

        if (!membership.AllowsRoleChanges)
        {
            return new ReplaceMembershipRolesResult.InvalidMembershipStatus(membership.Status);
        }

        string[] previousRoleIds =
        [
            .. membership.RoleAssignments.Select(role => role.RoleId).Order(StringComparer.Ordinal),
        ];
        if (previousRoleIds.SequenceEqual(roleIds, StringComparer.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.Updated(command.MembershipId, roleIds);
        }

        bool removesAdministrator =
            previousRoleIds.Contains(
                SystemRoleIds.OrganizationAdministrator,
                StringComparer.Ordinal
            ) && !roleIds.Contains(SystemRoleIds.OrganizationAdministrator, StringComparer.Ordinal);
        if (
            removesAdministrator
            && !await consistency.HasAnotherActiveAdministratorAsync(
                command.OrganizationId.Value,
                membership.Id,
                cancellationToken
            )
        )
        {
            await RecordDenialAsync(
                command,
                roleIds,
                AccessAuditReasonCodes.LastAdministrator,
                cancellationToken
            );
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.LastAdministrator();
        }

        DateTimeOffset changedAt = timeProvider.GetUtcNow();
        membership.ReplaceRoles(roleIds.ToHashSet(StringComparer.Ordinal), changedAt);
        context.AuditEntries.Add(
            MembershipAuditEntries.RolesReplaced(
                membership,
                command.ActorUserId.Value,
                previousRoleIds,
                roleIds,
                changedAt
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReplaceMembershipRolesResult.Updated(command.MembershipId, roleIds);
    }

    private async Task RecordDenialAsync(
        ReplaceMembershipRolesCommand command,
        IReadOnlyCollection<string> roleIds,
        string reasonCode,
        CancellationToken cancellationToken
    )
    {
        context.AuditEntries.Add(
            MembershipAuditEntries.RoleReplacementDenied(
                command.OrganizationId.Value,
                command.MembershipId.Value,
                command.ActorUserId.Value,
                reasonCode,
                roleIds,
                timeProvider.GetUtcNow()
            )
        );
        await context.SaveChangesAsync(cancellationToken);
    }
}
