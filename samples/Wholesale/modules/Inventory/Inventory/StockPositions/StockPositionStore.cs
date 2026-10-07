using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal sealed class StockPositionStore
    : EventStore<StockPositionAggregate, IStockPositionEvent, EventStream, StoredEvent>
{
    private readonly InventoryDbContext database;

    public StockPositionStore(InventoryDbContext database, TimeProvider timeProvider)
        : base(database, new StockPositionEventRecordAdapter(), timeProvider)
    {
        this.database = database;
        ConfigureMainState(new MainState());
    }

    protected override EventStream CreateStream(Guid id) =>
        new() { OrganizationKey = database.RequiredOrganizationKey };

    private sealed class MainState
        : InlineAggregateAdapter<StockPositionAggregate, StockPositionCurrentRow>
    {
        public override StockPositionAggregate Restore(StockPositionCurrentRow state) =>
            StockPositionAggregate.FromState(state.StreamId, state.Version, state.ReadState());

        public override StockPositionCurrentRow CreateRecord(StockPositionAggregate aggregate) =>
            StockPositionCurrentRow.PrepareCandidate(aggregate.State!);
    }
}
