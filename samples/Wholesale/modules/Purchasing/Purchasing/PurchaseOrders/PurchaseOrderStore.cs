using ModulithFoundry.EventSourcing.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderStore
    : EventStore<PurchaseOrderAggregate, IPurchaseOrderEvent, EventStream, StoredEvent>
{
    private readonly PurchasingDbContext database;

    public PurchaseOrderStore(PurchasingDbContext database, TimeProvider timeProvider)
        : base(database, new PurchaseOrderEventRecordAdapter(), timeProvider)
    {
        this.database = database;
        ConfigureMainState(new MainState());
        ConfigureRequiredProjection(new Summary());
    }

    protected override EventStream CreateStream(Guid id) =>
        new() { OrganizationKey = database.RequiredOrganizationKey };

    private sealed class MainState
        : InlineAggregateAdapter<PurchaseOrderAggregate, PurchaseOrderCurrentRow>
    {
        public override PurchaseOrderAggregate Restore(PurchaseOrderCurrentRow state) =>
            PurchaseOrderAggregate.FromState(state.StreamId, state.Version, state.ReadState());

        public override PurchaseOrderCurrentRow CreateRecord(PurchaseOrderAggregate aggregate) =>
            PurchaseOrderCurrentRow.PrepareCandidate(aggregate.State!);
    }

    private sealed class Summary
        : InlineEventProjection<IPurchaseOrderEvent, PurchaseOrderSummaryRow>
    {
        public override PurchaseOrderSummaryRow Evolve(
            PurchaseOrderSummaryRow? committed,
            IReadOnlyList<IPurchaseOrderEvent> events
        ) => PurchaseOrderSummaryRow.Evolve(committed, events);
    }
}
