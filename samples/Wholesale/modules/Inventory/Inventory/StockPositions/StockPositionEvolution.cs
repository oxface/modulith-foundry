using ModulithFoundry.Samples.Wholesale.Inventory.Contracts;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class StockPositionEvolution
{
    internal static StockPositionHistory Rehydrate(
        Guid id,
        long version,
        DateTimeOffset recordedAt,
        IEnumerable<IStockPositionEvent> events
    ) => Apply(null, id, version, recordedAt, events);

    internal static StockPositionHistory Apply(
        StockPositionHistory? state,
        Guid id,
        long version,
        DateTimeOffset recordedAt,
        IEnumerable<IStockPositionEvent> events
    )
    {
        foreach (IStockPositionEvent @event in events)
            state = @event switch
            {
                StockPositionOpened opened
                    when state is null
                        && opened.StockItemId != Guid.Empty
                        && opened.StockingLocationId != Guid.Empty
                        && !string.IsNullOrWhiteSpace(opened.BaseUnitCode) =>
                    new StockPositionHistory(
                        id,
                        version,
                        recordedAt,
                        opened.StockItemId,
                        opened.StockingLocationId,
                        opened.BaseUnitCode,
                        0,
                        null
                    ),
                StockPositionReceived received when state is not null && received.Quantity > 0 =>
                    state with
                    {
                        OnHand = state.OnHand + received.Quantity,
                        LatestDeliveryReference = received.DeliveryReference,
                    },
                _ => throw new InvalidOperationException(
                    "Invalid stock-position event sequence or business values."
                ),
            };
        return (
            state ?? throw new InvalidOperationException("The stock position has not been opened.")
        ) with
        {
            Version = version,
            RecordedAt = recordedAt,
        };
    }
}
