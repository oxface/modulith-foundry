namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class MembershipRoleAssignment
{
    private MembershipRoleAssignment()
    {
        RoleId = null!;
    }

    private MembershipRoleAssignment(Guid membershipId, string roleId, DateTimeOffset assignedAt)
    {
        MembershipId = membershipId;
        RoleId = roleId;
        AssignedAt = assignedAt;
    }

    internal Guid MembershipId { get; private set; }

    internal string RoleId { get; private set; }

    internal DateTimeOffset AssignedAt { get; private set; }

    internal static MembershipRoleAssignment Create(
        Guid membershipId,
        string roleId,
        DateTimeOffset assignedAt) =>
        new(membershipId, roleId, assignedAt);
}
