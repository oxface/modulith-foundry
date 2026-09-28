using System.Collections.Frozen;
using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Access.Organizations;

internal sealed class SystemRoleCatalog(IEnumerable<string> productRoleIds)
{
    private readonly FrozenSet<string> _roleIds = productRoleIds
        .Append(SystemRoleIds.OrganizationAdministrator)
        .Where(static roleId => !string.IsNullOrWhiteSpace(roleId))
        .ToFrozenSet(StringComparer.Ordinal);

    internal bool Contains(string roleId) => _roleIds.Contains(roleId);
}
