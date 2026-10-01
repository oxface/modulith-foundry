namespace ModulithFoundry.Modules.Access.Contracts;

public sealed record SystemRoleDefinition(
    string Id,
    string DisplayName,
    IReadOnlyList<string> PermissionIds
);
