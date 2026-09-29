namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record SystemRoleManifest(
    string ModuleId,
    IReadOnlyList<SystemPermissionDefinition> Permissions,
    IReadOnlyList<SystemRoleDefinition> Roles);
