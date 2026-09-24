using ModulithFoundry.Modules.Access.Organizations;

namespace ModulithFoundry.Modules.Access.Organizations.Queries;

internal static class MembershipQueryExtensions
{
    internal static IQueryable<Membership> Active(this IQueryable<Membership> query) =>
        query.Where(membership => membership.Status == MembershipStatus.Active);
}
