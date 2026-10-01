namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesOrderActivityKindValues
{
    public const string Created = "created";
    public const string Submitted = "submitted";
    public const string Approved = "approved";

    public static string ToValue(SalesOrderActivityKind kind) =>
        kind switch
        {
            SalesOrderActivityKind.Created => Created,
            SalesOrderActivityKind.Submitted => Submitted,
            SalesOrderActivityKind.Approved => Approved,
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                "Unknown sales order activity kind."
            ),
        };

    public static SalesOrderActivityKind FromValue(string value) =>
        value switch
        {
            Created => SalesOrderActivityKind.Created,
            Submitted => SalesOrderActivityKind.Submitted,
            Approved => SalesOrderActivityKind.Approved,
            _ => throw new InvalidOperationException(
                $"Unknown sales order activity kind '{value}'."
            ),
        };
}
