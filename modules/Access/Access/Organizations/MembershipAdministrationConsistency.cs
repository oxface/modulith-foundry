using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations.Queries;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class MembershipAdministrationConsistency(AccessDbContext context)
{
    internal async Task<bool> LockOrganizationAsync(
        Guid organizationId,
        CancellationToken cancellationToken
    ) =>
        await context
            .Organizations.FromSqlInterpolated(
                $$"""
                SELECT id, name, slug, created_at
                FROM access.organizations
                WHERE id = {{organizationId}}
                FOR UPDATE
                """
            )
            .AnyAsync(cancellationToken);

    internal Task<bool> HasAnotherActiveAdministratorAsync(
        Guid organizationId,
        Guid excludedMembershipId,
        CancellationToken cancellationToken
    ) =>
        (
            from membership in context
                .Memberships.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                .Active()
            join role in context.MembershipRoleAssignments.IgnoreQueryFilters([
                AccessDbContext.OrganizationScopeFilter,
            ])
                on new { MembershipId = membership.Id, membership.OrganizationId } equals new
                {
                    role.MembershipId,
                    role.OrganizationId,
                }
            where
                membership.OrganizationId == organizationId
                && membership.Id != excludedMembershipId
                && role.RoleId == SystemRoleIds.OrganizationAdministrator
            select membership.Id
        ).AnyAsync(cancellationToken);
}
