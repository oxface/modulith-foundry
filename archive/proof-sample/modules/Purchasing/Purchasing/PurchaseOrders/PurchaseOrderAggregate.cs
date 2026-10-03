using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Events;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderAggregate
{
    private readonly List<IPurchaseOrderEvent> pending = [];

    private PurchaseOrderAggregate(Guid id, long version, PurchaseOrderState? state)
    {
        Id = id;
        ExpectedVersion = Version = version;
        State = state;
    }

    internal Guid Id { get; }
    internal long ExpectedVersion { get; }
    internal long Version { get; private set; }
    internal PurchaseOrderState? State { get; private set; }
    internal IReadOnlyList<IPurchaseOrderEvent> Pending => pending;

    internal static PurchaseOrderAggregate Empty(Guid id) => new(id, 0, null);

    internal static PurchaseOrderAggregate FromState(
        Guid id,
        long version,
        PurchaseOrderState state
    ) => new(id, version, state);

    internal void Draft(string code, string supplierReference, string currency) =>
        Accept([PurchaseOrderDecider.Draft(code, supplierReference, currency)]);

    internal void SetLine(string itemCode, decimal quantity, decimal unitPrice) =>
        Accept(
            PurchaseOrderDecider.SetLine(
                State ?? throw new InvalidOperationException("Order must be drafted."),
                itemCode,
                quantity,
                unitPrice
            )
        );

    internal void Issue() =>
        Accept(
            PurchaseOrderDecider.Issue(
                State ?? throw new InvalidOperationException("Order must be drafted.")
            )
        );

    private void Accept(IReadOnlyList<IPurchaseOrderEvent> events)
    {
        var candidate = State;
        foreach (var @event in events)
            candidate = PurchaseOrderEvolution.Evolve(candidate, @event);
        // Validate a new decision's complete candidate, never historical intermediate state.
        if (
            candidate is null
            || (candidate.Status == PurchaseOrderStatus.Issued && candidate.Lines.Count == 0)
        )
            throw new PurchaseOrderDecisionException("An issued Purchase Order requires lines.");
        State = candidate;
        Version += events.Count;
        pending.AddRange(events);
    }
}
