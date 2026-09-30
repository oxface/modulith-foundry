namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockPositionHistoryEntry(
    long Version,
    DateTimeOffset RecordedAt,
    StockPositionHistoryAction Action,
    decimal? Quantity);
