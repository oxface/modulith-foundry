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
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
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
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
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

    public async Task<OrganizationAccessContext?> ResolveAccessAsync(
        UserId userId,
        string organizationSlug,
        CancellationToken cancellationToken = default)
    {
        OrganizationSlug slug;
        try
        {
            slug = OrganizationSlug.Create(organizationSlug);
        }
        catch (InvalidOrganizationSlugException)
        {
            return null;
        }

        var access = await (
            from membership in context.Set<Membership>()
                .AsNoTracking()
                .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            join organization in context.Set<Organization>().AsNoTracking()
                on membership.OrganizationId equals organization.Id
            where membership.UserId == userId.Value
                && membership.Status == MembershipStatus.Active
                && organization.Slug == slug
            select new
            {
                MembershipId = membership.Id,
                OrganizationId = organization.Id,
                organization.Name,
                organization.Slug,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (access is null)
        {
            return null;
        }

        string[] roles = await context.Set<MembershipRoleAssignment>()
            .AsNoTracking()
            .IgnoreQueryFilters([AccessDbContext.OrganizationScopeFilter])
            .Where(role => role.MembershipId == access.MembershipId)
            .Where(role => role.OrganizationId == access.OrganizationId)
            .OrderBy(role => role.RoleId)
            .Select(role => role.RoleId)
            .ToArrayAsync(cancellationToken);

        return new OrganizationAccessContext(
            userId,
            new OrganizationId(access.OrganizationId),
            new MembershipId(access.MembershipId),
            access.Name,
            access.Slug.Value,
            roles);
    }
}
