using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class MembershipRoleAssignment : IOrganizationOwned
{
    private MembershipRoleAssignment()
    {
        RoleId = null!;
    }

    private MembershipRoleAssignment(
        Guid membershipId,
        Guid organizationId,
        string roleId,
        DateTimeOffset assignedAt)
    {
        MembershipId = membershipId;
        RoleId = roleId;
        AssignedAt = assignedAt;
        OrganizationId = organizationId;
    }

    internal Guid MembershipId { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal string RoleId { get; private set; }

    internal DateTimeOffset AssignedAt { get; private set; }

    internal static MembershipRoleAssignment Create(
        Guid membershipId,
        Guid organizationId,
        string roleId,
        DateTimeOffset assignedAt) =>
        new(membershipId, organizationId, roleId, assignedAt);
}
