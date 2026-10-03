namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record RecordStockReceiptResult
{
    private RecordStockReceiptResult() { }

    public sealed record Recorded(StockPositionView Position) : RecordStockReceiptResult;

    public sealed record Invalid(string Field, string Detail) : RecordStockReceiptResult;

    public sealed record ReferenceUnavailable(string Reference) : RecordStockReceiptResult;

    public sealed record VersionConflict : RecordStockReceiptResult;

    public sealed record PermissionDenied : RecordStockReceiptResult;
}
