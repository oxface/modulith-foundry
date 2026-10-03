using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.Queries;

internal sealed class OrganizationMembershipQueries(
    AccessDbContext context,
    SystemRoleCatalog roleCatalog
) : IOrganizationMembershipQueries, IOrganizationAuthorization
{
    public Task<bool> IsActiveAsync(
        OrganizationId organizationId,
        MembershipId membershipId,
        CancellationToken cancellationToken = default
    ) =>
        context
            .Memberships.AsNoTracking()
            .Active()
            .AnyAsync(
                membership =>
                    membership.OrganizationId == organizationId.Value
                    && membership.Id == membershipId.Value,
                cancellationToken
            );

    public Task<bool> HasPermissionAsync(
        UserId userId,
        OrganizationId organizationId,
        string permissionId,
        CancellationToken cancellationToken = default
    ) => HasPermissionAsync(userId.Value, organizationId.Value, permissionId, cancellationToken);

    internal async Task<bool> HasPermissionAsync(
        Guid actorUserId,
        Guid organizationId,
        string permissionId,
        CancellationToken cancellationToken
    ) =>
        (
            await (
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
                    membership.OrganizationId == organizationId && membership.UserId == actorUserId
                select role.RoleId
            ).ToArrayAsync(cancellationToken)
        ).Any(roleId => roleCatalog.Grants(roleId, permissionId));

    public async Task<ListOrganizationMembersResult> ListForAdministrationAsync(
        ListOrganizationMembersQuery query,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(query);

        if (
            !await HasPermissionAsync(
                query.ActorUserId.Value,
                query.OrganizationId.Value,
                AccessPermissionIds.MembersManage,
                cancellationToken
            )
        )
        {
            return new ListOrganizationMembersResult.PermissionDenied();
        }

        var members = await (
            from membership in context
                .Memberships.AsNoTracking()
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                .Current()
            join user in context.Users.AsNoTracking() on membership.UserId equals user.Id
            where membership.OrganizationId == query.OrganizationId.Value
            orderby user.DisplayName, user.Email, membership.Id
            select new
            {
                MembershipId = membership.Id,
                UserId = user.Id,
                user.Email,
                user.DisplayName,
                membership.Status,
            }
        ).ToListAsync(cancellationToken);

        Guid[] membershipIds = [.. members.Select(member => member.MembershipId)];
        Dictionary<Guid, IReadOnlyList<string>> rolesByMembership = (
            await context
                .MembershipRoleAssignments.AsNoTracking()
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                .Where(role => membershipIds.Contains(role.MembershipId))
                .OrderBy(role => role.RoleId)
                .ToListAsync(cancellationToken)
        )
            .GroupBy(role => role.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(role => role.RoleId)]
            );

        var view = new OrganizationMembershipAdministration(
            [
                .. members.Select(member => new OrganizationMember(
                    new MembershipId(member.MembershipId),
                    new UserId(member.UserId),
                    member.Email,
                    member.DisplayName,
                    member.Status,
                    rolesByMembership.GetValueOrDefault(member.MembershipId, [])
                )),
            ],
            roleCatalog.Roles,
            roleCatalog.Permissions
        );
        return new ListOrganizationMembersResult.Listed(view);
    }

    internal Task<bool> HasCurrentMembershipForEmailAsync(
        Guid organizationId,
        string normalizedEmail,
        CancellationToken cancellationToken
    ) =>
        (
            from membership in context
                .Memberships.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                .Current()
            join user in context.Users on membership.UserId equals user.Id
            where
                membership.OrganizationId == organizationId
                && user.Email != null
                && EF.Functions.ILike(user.Email, EscapeLikePattern(normalizedEmail), @"\")
            select membership.Id
        ).AnyAsync(cancellationToken);

    internal Task<bool> HasCurrentMembershipAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken
    ) =>
        context
            .Memberships.IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Current()
            .AnyAsync(
                membership =>
                    membership.OrganizationId == organizationId && membership.UserId == userId,
                cancellationToken
            );

    private static string EscapeLikePattern(string value) =>
        value
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
