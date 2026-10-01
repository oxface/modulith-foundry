namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record ReplaceMembershipRolesCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    MembershipId MembershipId,
    IReadOnlyCollection<string> RoleIds
);
