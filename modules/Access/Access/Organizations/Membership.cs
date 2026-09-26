using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal enum MembershipStatus
{
    Active,
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

    private void AssignRole(string roleId, DateTimeOffset assignedAt) =>
        _roleAssignments.Add(MembershipRoleAssignment.Create(
            Id,
            OrganizationId,
            roleId,
            assignedAt));
}
