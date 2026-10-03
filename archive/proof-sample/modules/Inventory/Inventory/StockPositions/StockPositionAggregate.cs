using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions;

internal sealed class StockPositionAggregate
{
    private readonly List<IStockPositionEvent> uncommittedEvents = [];

    private StockPositionAggregate(Guid streamId, long version, StockPositionState? state)
    {
        StreamId = streamId;
        Version = version;
        State = state;
    }

    internal Guid StreamId { get; }

    internal long ExpectedVersion => Version - uncommittedEvents.Count;

    internal long Version { get; private set; }

    internal StockPositionState? State { get; private set; }

    internal IReadOnlyList<IStockPositionEvent> UncommittedEvents => uncommittedEvents;

    internal static StockPositionAggregate Empty(Guid streamId) => new(streamId, 0, state: null);

    internal static StockPositionAggregate FromState(
        Guid streamId,
        long version,
        StockPositionState state
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentNullException.ThrowIfNull(state);
        return new(streamId, version, state);
    }

    internal static StockPositionAggregate Rehydrate(
        Guid streamId,
        IReadOnlyList<IStockPositionEvent> events
    )
    {
        var aggregate = Empty(streamId);
        foreach (IStockPositionEvent @event in events)
        {
            aggregate.ApplyHistorical(@event);
        }

        return aggregate;
    }

    internal void RecordReceipt(
        Guid stockItemId,
        Guid stockingLocationId,
        string baseUnitCode,
        decimal quantity
    )
    {
        Quantity validated = Quantity.Positive(quantity);
        IReadOnlyList<IStockPositionEvent> events = StockPositionDecider.DecideReceipt(
            State,
            stockItemId,
            stockingLocationId,
            baseUnitCode,
            validated
        );
        AcceptDecision(events);
    }

    internal void AcceptDecision(IReadOnlyList<IStockPositionEvent> events)
    {
        StockPositionState? candidate = State;
        foreach (IStockPositionEvent @event in events)
        {
            candidate = StockPositionEvolution.Evolve(candidate, @event);
        }

        StockPositionPolicy.Validate(candidate);
        State = candidate;
        uncommittedEvents.AddRange(events);
        Version += events.Count;
    }

    internal void CorrectQuantity(decimal onHandQuantity, string reason)
    {
        if (State is null)
        {
            throw new InvalidOperationException("Cannot correct an unopened Stock Position.");
        }
        AcceptDecision(
            StockPositionDecider.DecideCorrection(
                State,
                Quantity.NonNegative(onHandQuantity),
                StockCorrectionReason.Create(reason)
            )
        );
    }

    internal bool TryReserve(Guid reservationId, Guid operationId, decimal quantity)
    {
        Quantity requested = Quantity.Positive(quantity);
        IReadOnlyList<IStockPositionEvent> decision = StockPositionDecider.DecideReservation(
            State,
            reservationId,
            operationId,
            requested
        );
        if (decision.Count == 0)
            return false;
        AcceptDecision(decision);
        return true;
    }

    internal bool ReleaseReservation(Guid reservationId, Guid operationId)
    {
        if (State is null)
            throw new InvalidOperationException("Cannot release from an unopened Stock Position.");
        var decision = StockPositionDecider.DecideRelease(State, reservationId, operationId);
        if (decision.Count == 0)
            return false;
        AcceptDecision(decision);
        return true;
    }

    private void ApplyHistorical(IStockPositionEvent @event)
    {
        State = StockPositionEvolution.Evolve(State, @event);
        Version++;
    }
}
