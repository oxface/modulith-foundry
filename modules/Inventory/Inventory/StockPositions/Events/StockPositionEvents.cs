using System.Text.Json.Serialization;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Events;

[StoredEventType("inventory.stock-position.opened", 1)]
internal sealed record StockPositionOpened(
    [property: JsonRequired] Guid StockItemId,
    [property: JsonRequired] Guid StockingLocationId,
    [property: JsonRequired] string BaseUnitCode
) : IStockPositionEvent;

[StoredEventType("inventory.stock-position.received", 1)]
internal sealed record StockReceived([property: JsonRequired] decimal Quantity)
    : IStockPositionEvent;

[StoredEventType("inventory.stock-position.quantity-corrected", 1)]
internal sealed record StockQuantityCorrected(
    [property: JsonRequired] decimal OnHandQuantity,
    [property: JsonRequired] string Reason
) : IStockPositionEvent;
