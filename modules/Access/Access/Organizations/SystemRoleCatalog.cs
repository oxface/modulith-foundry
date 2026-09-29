using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class SystemRoleCatalog
{
    private readonly IReadOnlyDictionary<string, SystemRoleDefinition> _roles;

    internal SystemRoleCatalog(IEnumerable<SystemRoleManifest> productManifests)
    {
        SystemRoleManifest[] manifests =
        [
            new(
                "access",
                [new(AccessPermissionIds.MembersManage, "Manage organization access")],
                [
                    new(
                        SystemRoleIds.OrganizationAdministrator,
                        "Organization Administrator",
                        [AccessPermissionIds.MembersManage]),
                ]),
            .. productManifests,
        ];

        Validate(manifests);
        Permissions = manifests
            .SelectMany(manifest => manifest.Permissions)
            .Select(Copy)
            .OrderBy(permission => permission.Id, StringComparer.Ordinal)
            .ToArray();
        _roles = manifests
            .SelectMany(manifest => manifest.Roles)
            .Select(Copy)
            .OrderBy(role => role.Id, StringComparer.Ordinal)
            .ToDictionary(role => role.Id, StringComparer.Ordinal);
    }

    internal IReadOnlyList<SystemRoleDefinition> Roles => [.. _roles.Values];

    internal IReadOnlyList<SystemPermissionDefinition> Permissions { get; }

    internal bool Contains(string roleId) => _roles.ContainsKey(roleId);

    internal bool Grants(string roleId, string permissionId) =>
        _roles.TryGetValue(roleId, out SystemRoleDefinition? role)
        && role.PermissionIds.Contains(permissionId, StringComparer.Ordinal);

    private static SystemPermissionDefinition Copy(SystemPermissionDefinition permission) =>
        new(permission.Id, permission.DisplayName);

    private static SystemRoleDefinition Copy(SystemRoleDefinition role) =>
        new(role.Id, role.DisplayName, [.. role.PermissionIds]);

    private static void Validate(IReadOnlyCollection<SystemRoleManifest> manifests)
    {
        EnsureUnique(
            manifests.Select(manifest => manifest.ModuleId),
            "authorization manifest module IDs");
        if (manifests.Any(manifest => string.IsNullOrWhiteSpace(manifest.ModuleId)))
        {
            throw new InvalidOperationException("An authorization manifest has no module ID.");
        }

        SystemPermissionDefinition[] permissions =
            [.. manifests.SelectMany(manifest => manifest.Permissions)];
        EnsureUnique(
            permissions.Select(permission => permission.Id),
            "system permission IDs");
        if (permissions.Any(permission =>
            string.IsNullOrWhiteSpace(permission.Id)
            || string.IsNullOrWhiteSpace(permission.DisplayName)))
        {
            throw new InvalidOperationException("A system permission has an invalid definition.");
        }

        SystemRoleDefinition[] roles = [.. manifests.SelectMany(manifest => manifest.Roles)];
        EnsureUnique(roles.Select(role => role.Id), "system role IDs");
        foreach (SystemRoleManifest manifest in manifests)
        {
            HashSet<string> ownedPermissionIds = manifest.Permissions
                .Select(permission => permission.Id)
                .ToHashSet(StringComparer.Ordinal);
            foreach (SystemRoleDefinition role in manifest.Roles)
            {
                if (string.IsNullOrWhiteSpace(role.Id)
                    || string.IsNullOrWhiteSpace(role.DisplayName)
                    || role.PermissionIds.Count == 0
                    || role.PermissionIds.Any(permissionId =>
                        !ownedPermissionIds.Contains(permissionId))
                    || role.PermissionIds.Distinct(StringComparer.Ordinal).Count()
                        != role.PermissionIds.Count)
                {
                    throw new InvalidOperationException(
                        $"System role '{role.Id}' has an invalid definition.");
                }
            }
        }
    }

    private static void EnsureUnique(IEnumerable<string> values, string description)
    {
        string[] duplicates =
        [.. values
            .GroupBy(value => value, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate {description}: {string.Join(", ", duplicates)}.");
        }
    }
}
