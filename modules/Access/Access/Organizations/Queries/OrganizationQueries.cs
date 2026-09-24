using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.Queries;

internal sealed class OrganizationQueries(AccessDbContext context) : IOrganizationQueries
{
    public async Task<IReadOnlyList<OrganizationMembership>> ListAccessibleToAsync(
        UserId userId,
        CancellationToken cancellationToken = default)
    {
        var memberships = await (
            from membership in context.Set<Membership>()
                .AsNoTracking()
                .Where(membership => membership.Status == MembershipStatus.Active)
                .Where(membership => membership.UserId == userId.Value)
            join organization in context.Set<Organization>().AsNoTracking()
                on membership.OrganizationId equals organization.Id
            orderby organization.Name, organization.Id
            select new
            {
                MembershipId = membership.Id,
                OrganizationId = organization.Id,
                organization.Name,
                organization.Slug,
            })
            .ToListAsync(cancellationToken);
        if (memberships.Count == 0)
        {
            return [];
        }

        Guid[] membershipIds = [.. memberships.Select(row => row.MembershipId)];
        var rolesByMembership = (await context.Set<MembershipRoleAssignment>()
                .AsNoTracking()
                .Where(role => membershipIds.Contains(role.MembershipId))
                .OrderBy(role => role.RoleId)
                .ToListAsync(cancellationToken))
            .GroupBy(role => role.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(role => role.RoleId)]);

        return memberships
            .Select(row => new OrganizationMembership(
                new OrganizationId(row.OrganizationId),
                row.Name,
                row.Slug.Value,
                rolesByMembership.GetValueOrDefault(row.MembershipId, [])))
            .ToArray();
    }
}
