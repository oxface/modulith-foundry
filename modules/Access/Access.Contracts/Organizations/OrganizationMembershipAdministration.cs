namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record OrganizationMembershipAdministration(
    IReadOnlyList<OrganizationMember> Members,
    IReadOnlyList<SystemRoleDefinition> SystemRoles,
    IReadOnlyList<SystemPermissionDefinition> SystemPermissions);
