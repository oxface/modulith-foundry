using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.ReplaceMembershipRoles;

internal sealed class ReplaceMembershipRolesHandler(
    AccessDbContext context,
    OrganizationMembershipQueries membershipQueries,
    SystemRoleCatalog roleCatalog,
    TimeProvider timeProvider) : IOrganizationMembershipAdministration
{
    public async Task<ReplaceMembershipRolesResult> ReplaceRolesAsync(
        ReplaceMembershipRolesCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.RoleIds);

        string[] roleIds = [.. command.RoleIds
            .Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        await using var transaction = await context.Database.BeginTransactionAsync(
            cancellationToken);
        bool organizationExists = await LockOrganizationAsync(
            command.OrganizationId.Value,
            cancellationToken);
        if (!organizationExists)
        {
            return new ReplaceMembershipRolesResult.NotFound();
        }

        if (!await membershipQueries.HasPermissionAsync(
                command.ActorUserId.Value,
                command.OrganizationId.Value,
                AccessPermissionIds.MembersManage,
                cancellationToken))
        {
            await RecordDenialAsync(
                command,
                roleIds,
                "permission-denied",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.PermissionDenied();
        }

        string[] invalidRoleIds = [.. roleIds.Where(roleId => !roleCatalog.Contains(roleId))];
        if (roleIds.Length == 0 || invalidRoleIds.Length > 0)
        {
            return new ReplaceMembershipRolesResult.InvalidRoles(invalidRoleIds);
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
            return new ReplaceMembershipRolesResult.NotFound();
        }

        string[] previousRoleIds = [.. membership.RoleAssignments
            .Select(role => role.RoleId)
            .Order(StringComparer.Ordinal)];
        if (previousRoleIds.SequenceEqual(roleIds, StringComparer.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.Updated(
                command.MembershipId,
                roleIds);
        }

        bool removesAdministrator = previousRoleIds.Contains(
                SystemRoleIds.OrganizationAdministrator,
                StringComparer.Ordinal)
            && !roleIds.Contains(SystemRoleIds.OrganizationAdministrator, StringComparer.Ordinal);
        if (removesAdministrator
            && !await HasAnotherAdministratorAsync(
                command.OrganizationId.Value,
                membership.Id,
                cancellationToken))
        {
            await RecordDenialAsync(
                command,
                roleIds,
                "last-administrator",
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new ReplaceMembershipRolesResult.LastAdministrator();
        }

        DateTimeOffset changedAt = timeProvider.GetUtcNow();
        membership.ReplaceRoles(roleIds.ToHashSet(StringComparer.Ordinal), changedAt);
        context.AuditEntries.Add(MembershipAuditEntries.RolesReplaced(
            membership,
            command.ActorUserId.Value,
            previousRoleIds,
            roleIds,
            changedAt));
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ReplaceMembershipRolesResult.Updated(
            command.MembershipId,
            roleIds);
    }

    private async Task<bool> LockOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await context.Organizations
            .FromSqlInterpolated($$"""
                SELECT id, name, slug, created_at
                FROM access.organizations
                WHERE id = {{organizationId}}
                FOR UPDATE
                """)
            .AnyAsync(cancellationToken);

    private Task<bool> HasAnotherAdministratorAsync(
        Guid organizationId,
        Guid excludedMembershipId,
        CancellationToken cancellationToken) =>
        (
            from membership in context.Memberships
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            join role in context.MembershipRoleAssignments
                    .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                on new { MembershipId = membership.Id, membership.OrganizationId }
                equals new { role.MembershipId, role.OrganizationId }
            where membership.OrganizationId == organizationId
                && membership.Id != excludedMembershipId
                && membership.Status == MembershipStatus.Active
                && role.RoleId == SystemRoleIds.OrganizationAdministrator
            select membership.Id)
            .AnyAsync(cancellationToken);

    private async Task RecordDenialAsync(
        ReplaceMembershipRolesCommand command,
        IReadOnlyCollection<string> roleIds,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        context.AuditEntries.Add(MembershipAuditEntries.RoleReplacementDenied(
            command.OrganizationId.Value,
            command.MembershipId.Value,
            command.ActorUserId.Value,
            reasonCode,
            roleIds,
            timeProvider.GetUtcNow()));
        await context.SaveChangesAsync(cancellationToken);
    }
}
