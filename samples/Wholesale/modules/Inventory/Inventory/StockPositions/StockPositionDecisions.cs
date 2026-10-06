using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class StockPositionDecisions
{
    internal static IStockPositionEvent[] Open(OpenStockPosition request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.Id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNotEqual(request.ExpectedVersion, 0);
        ArgumentOutOfRangeException.ThrowIfEqual(request.StockItemId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(request.StockingLocationId, Guid.Empty);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.BaseUnitCode);
        return
        [
            new StockPositionOpened(
                request.StockItemId,
                request.StockingLocationId,
                request.BaseUnitCode
            ),
        ];
    }

    internal static IStockPositionEvent[] Receipts(ReceiveStock request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentOutOfRangeException.ThrowIfEqual(request.Id, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfLessThan(request.ExpectedVersion, 1);
        ArgumentNullException.ThrowIfNull(request.Receipts);
        StockReceipt[] items = request.Receipts.ToArray();
        ArgumentOutOfRangeException.ThrowIfLessThan(items.Length, 1);
        return items
            .Select(item =>
            {
                ArgumentNullException.ThrowIfNull(item);
                ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(item.Quantity, 0);
                return (IStockPositionEvent)
                    new StockPositionReceived(item.Quantity, item.DeliveryReference);
            })
            .ToArray();
    }
}
