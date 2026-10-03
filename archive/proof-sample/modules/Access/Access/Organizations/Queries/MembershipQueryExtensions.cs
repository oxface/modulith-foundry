using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations.Queries;

internal static class MembershipQueryExtensions
{
    internal static IQueryable<Membership> Active(this IQueryable<Membership> memberships) =>
        memberships.Where(membership => membership.Status == MembershipStatus.Active);

    internal static IQueryable<Membership> Current(this IQueryable<Membership> memberships) =>
        memberships.Where(membership =>
            membership.Status == MembershipStatus.Active
            || membership.Status == MembershipStatus.Suspended
        );
}
