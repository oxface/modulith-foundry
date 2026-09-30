using ModulithFoundry.Modules.Inventory.StockPositions.Events;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.ApplicationTests;

public sealed class StockPositionEventRegistryTests
{
    [Fact]
    public void BuildRegistry_EventWithoutIdentity_Fails()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StockPositionEventSerializer.BuildRegistry([typeof(MissingIdentity)]));
    }

    [Fact]
    public void BuildRegistry_DuplicateDurableIdentity_Fails()
    {
        Assert.Throws<InvalidOperationException>(() =>
            StockPositionEventSerializer.BuildRegistry([typeof(FirstEvent), typeof(ConflictingEvent)]));
    }

    private sealed record MissingIdentity : IStockPositionEvent;

    [StoredEventType("test.same-event", 1)]
    private sealed record FirstEvent : IStockPositionEvent;

    [StoredEventType("test.same-event", 1)]
    private sealed record ConflictingEvent : IStockPositionEvent;
}
