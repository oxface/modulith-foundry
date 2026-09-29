using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal enum MembershipStatus
{
    Active = 1,
}

internal sealed class Membership : IOrganizationOwned
{
    private readonly List<MembershipRoleAssignment> _roleAssignments = [];

    private Membership()
    {
    }

    private Membership(
        Guid id,
        Guid organizationId,
        Guid userId,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        UserId = userId;
        Status = MembershipStatus.Active;
        CreatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal Guid UserId { get; private set; }

    internal MembershipStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal IReadOnlyCollection<MembershipRoleAssignment> RoleAssignments => _roleAssignments;

    internal static Membership CreateInitialAdministrator(
        Guid id,
        Guid organizationId,
        Guid userId,
        DateTimeOffset createdAt)
    {
        var membership = new Membership(id, organizationId, userId, createdAt);
        membership.AssignRole(SystemRoleIds.OrganizationAdministrator, createdAt);
        return membership;
    }

    internal static Membership CreateFromInvitation(
        Guid id,
        Guid organizationId,
        Guid userId,
        IReadOnlyCollection<string> roleIds,
        DateTimeOffset createdAt)
    {
        var membership = new Membership(id, organizationId, userId, createdAt);
        foreach (string roleId in roleIds)
        {
            membership.AssignRole(roleId, createdAt);
        }

        return membership;
    }

    internal bool HasRole(string roleId) =>
        _roleAssignments.Any(role => role.RoleId == roleId);

    internal void ReplaceRoles(
        IReadOnlySet<string> roleIds,
        DateTimeOffset assignedAt)
    {
        _roleAssignments.RemoveAll(role => !roleIds.Contains(role.RoleId));
        foreach (string roleId in roleIds.Where(roleId => !HasRole(roleId)))
        {
            AssignRole(roleId, assignedAt);
        }
    }

    private void AssignRole(string roleId, DateTimeOffset assignedAt) =>
        _roleAssignments.Add(MembershipRoleAssignment.Create(
            Id,
            OrganizationId,
            roleId,
            assignedAt));
}
