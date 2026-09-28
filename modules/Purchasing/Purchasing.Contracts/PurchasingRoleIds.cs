using System.Collections.Frozen;

namespace ModulithFoundry.Modules.Purchasing.Contracts;

public static class PurchasingRoleIds
{
    public const string Agent = "purchasing-agent";

    public static IReadOnlySet<string> All { get; } = new[] { Agent }
        .ToFrozenSet(StringComparer.Ordinal);
}
