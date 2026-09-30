using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.ApplicationTests;

public sealed class StockPositionDecisionTests
{
    [Fact]
    public void AcceptDecision_InvalidLaterEvent_LeavesStateVersionAndPendingEventsUnchanged()
    {
        StockPositionAggregate aggregate = StockPositionAggregate.Empty(Guid.NewGuid());
        aggregate.RecordReceipt(Guid.NewGuid(), Guid.NewGuid(), "EA", 10m);
        StockPositionState? original = aggregate.State;

        Assert.Throws<InvalidStockPositionValueException>(() => aggregate.AcceptDecision(
            [new StockReceived(2m), new StockReceived(-1m)]));

        Assert.Equal(original, aggregate.State);
        Assert.Equal(2, aggregate.Version);
        Assert.Equal(2, aggregate.UncommittedEvents.Count);
    }

    [Fact]
    public void Rehydrate_HistoricalEvents_ReconstructsStateWithoutPendingEvents()
    {
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(Guid.NewGuid(),
            [new StockPositionOpened(Guid.NewGuid(), Guid.NewGuid(), "EA"), new StockReceived(4m)]);

        Assert.Equal(4m, aggregate.State!.OnHand.Value);
        Assert.Equal(2, aggregate.ExpectedVersion);
        Assert.Empty(aggregate.UncommittedEvents);
    }
}
