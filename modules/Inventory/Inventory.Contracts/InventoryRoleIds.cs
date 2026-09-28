using System.Collections.Frozen;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public static class InventoryRoleIds
{
    public const string Manager = "inventory-manager";

    public static IReadOnlySet<string> All { get; } = new[] { Manager }
        .ToFrozenSet(StringComparer.Ordinal);
}
