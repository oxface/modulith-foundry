namespace ModulithFoundry.Modules.Sales.Contracts;

public static class SalesOrderActivityKindValues
{
    public const string Created = "created";
    public const string Submitted = "submitted";

    public static string ToValue(SalesOrderActivityKind kind) =>
        kind switch
        {
            SalesOrderActivityKind.Created => Created,
            SalesOrderActivityKind.Submitted => Submitted,
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
            _ => throw new InvalidOperationException(
                $"Unknown sales order activity kind '{value}'."
            ),
        };
}
