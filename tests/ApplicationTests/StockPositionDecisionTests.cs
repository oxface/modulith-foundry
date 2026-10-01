using ModulithFoundry.Modules.Inventory.StockPositions;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.ApplicationTests;

public sealed class StockPositionDecisionTests
{
    [Fact]
    public void AcceptDecision_InvalidFinalState_LeavesStateVersionAndPendingEventsUnchanged()
    {
        StockPositionAggregate aggregate = StockPositionAggregate.Empty(Guid.NewGuid());
        aggregate.RecordReceipt(Guid.NewGuid(), Guid.NewGuid(), "EA", 10m);
        StockPositionState? original = aggregate.State;

        Assert.Throws<InvalidStockPositionValueException>(() =>
            aggregate.AcceptDecision([
                new StockQuantityCorrected(2m, "Count"),
                new StockQuantityCorrected(-1m, "Invalid final state"),
            ])
        );

        Assert.Equal(original, aggregate.State);
        Assert.Equal(2, aggregate.Version);
        Assert.Equal(2, aggregate.UncommittedEvents.Count);
    }

    [Fact]
    public void AcceptDecision_InvalidIntermediateStateButValidFinalState_AcceptsWholeBatch()
    {
        StockPositionAggregate aggregate = StockPositionAggregate.FromState(
            Guid.NewGuid(),
            2,
            new(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "EA",
                Quantity.NonNegative(10m),
                Quantity.NonNegative(4m)
            )
        );

        aggregate.AcceptDecision([
            new StockQuantityCorrected(-1m, "Intermediate state"),
            new StockQuantityCorrected(7m, "Final state"),
        ]);

        Assert.Equal(7m, aggregate.State!.OnHand.Value);
        Assert.Equal(3m, aggregate.State.Available.Value);
        Assert.Equal(4, aggregate.Version);
        Assert.Equal(2, aggregate.UncommittedEvents.Count);
    }

    [Fact]
    public void Rehydrate_HistoricalReasonAndIntermediateQuantity_DoesNotRunCurrentInputRules()
    {
        Guid streamId = Guid.NewGuid();
        var opening = new StockPositionOpened(Guid.NewGuid(), Guid.NewGuid(), "EA");
        var correction = new StockQuantityCorrected(-1m, new string('x', 201));
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(
            streamId,
            [opening, correction]
        );

        Assert.Equal(-1m, aggregate.State!.OnHand.Value);
        Assert.Equal(-1m, aggregate.State.Available.Value);
        Assert.Equal(2, aggregate.Version);
        Assert.Empty(aggregate.UncommittedEvents);
    }

    [Fact]
    public void Rehydrate_HistoricalEvents_ReconstructsStateWithoutPendingEvents()
    {
        StockPositionAggregate aggregate = StockPositionAggregate.Rehydrate(
            Guid.NewGuid(),
            [new StockPositionOpened(Guid.NewGuid(), Guid.NewGuid(), "EA"), new StockReceived(4m)]
        );

        Assert.Equal(4m, aggregate.State!.OnHand.Value);
        Assert.Equal(2, aggregate.ExpectedVersion);
        Assert.Empty(aggregate.UncommittedEvents);
    }
}
