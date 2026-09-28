using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.Queries;

internal sealed class OrganizationMembershipQueries(AccessDbContext context)
{
    internal Task<bool> IsAdministratorAsync(
        Guid actorUserId,
        Guid organizationId,
        CancellationToken cancellationToken) =>
        (
            from membership in context.Memberships
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            join role in context.MembershipRoleAssignments
                    .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
                on new { MembershipId = membership.Id, membership.OrganizationId }
                equals new { role.MembershipId, role.OrganizationId }
            where membership.OrganizationId == organizationId
                && membership.UserId == actorUserId
                && membership.Status == MembershipStatus.Active
                && role.RoleId == SystemRoleIds.OrganizationAdministrator
            select membership.Id)
            .AnyAsync(cancellationToken);

    internal Task<bool> HasActiveMembershipForEmailAsync(
        Guid organizationId,
        string normalizedEmail,
        CancellationToken cancellationToken) =>
        (
            from membership in context.Memberships
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            join user in context.Users on membership.UserId equals user.Id
            where membership.OrganizationId == organizationId
                && membership.Status == MembershipStatus.Active
                && user.Email != null
                && EF.Functions.ILike(user.Email, EscapeLikePattern(normalizedEmail), @"\")
            select membership.Id)
            .AnyAsync(cancellationToken);

    private static string EscapeLikePattern(string value) =>
        value.Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
