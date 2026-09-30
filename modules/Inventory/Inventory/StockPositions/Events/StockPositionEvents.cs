namespace ModulithFoundry.Modules.Inventory.StockPositions.Events;

[StoredEventType("inventory.stock-position.opened", 1)]
internal sealed record StockPositionOpened(
    Guid StockItemId,
    Guid StockingLocationId,
    string BaseUnitCode) : IStockPositionEvent;

[StoredEventType("inventory.stock-position.received", 1)]
internal sealed record StockReceived(decimal Quantity) : IStockPositionEvent;
