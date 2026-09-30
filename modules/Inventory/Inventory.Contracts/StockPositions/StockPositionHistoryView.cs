namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockPositionHistoryView(
    StockPositionId StockPositionId,
    string BaseUnitCode,
    long Version,
    IReadOnlyList<StockPositionHistoryEntry> Entries,
    long? NextAfterVersion);
