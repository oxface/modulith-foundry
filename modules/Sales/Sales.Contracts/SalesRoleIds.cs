using System.Collections.Frozen;

namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesRoleIds
{
    public const string Clerk = "sales-clerk";
    public const string Manager = "sales-manager";
    public const string Approver = "sales-approver";

    public static IReadOnlySet<string> All { get; } = new[] { Clerk, Manager, Approver }
        .ToFrozenSet(StringComparer.Ordinal);
}
