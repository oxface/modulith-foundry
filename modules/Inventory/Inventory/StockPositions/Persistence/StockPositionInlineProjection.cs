using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Persistence;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionInlineProjection(InventoryDbContext context)
{
    internal async Task StageAsync(
        Guid organizationId, Guid streamId, long expectedVersion,
        IReadOnlyList<IStockPositionEvent> events, DateTimeOffset recordedAt,
        CancellationToken cancellationToken)
    {
        StockPositionCurrent? current = null;
        if (expectedVersion > 0)
        {
            current = await context.StockPositionCurrent.SingleOrDefaultAsync(
                position => position.StreamId == streamId, cancellationToken);
            if (current is null || current.Version < expectedVersion)
            {
                throw new InvalidOperationException(
                    "Stock Position projection is missing or behind its stream; rebuild is required.");
            }

            if (current.Version > expectedVersion)
            {
                throw new DbUpdateConcurrencyException("Stock Position projection has advanced concurrently.");
            }
        }

        StockPositionState? state = current?.ToState();
        foreach (IStockPositionEvent @event in events)
        {
            state = StockPositionDecider.Evolve(state, @event);
        }

        if (state is null)
        {
            throw new InvalidOperationException("Cannot project an empty Stock Position.");
        }

        long version = expectedVersion + events.Count;
        if (current is null)
        {
            context.StockPositionCurrent.Add(StockPositionCurrent.Create(
                streamId, organizationId, state, version, recordedAt));
        }
        else
        {
            current.Update(state, version, recordedAt);
        }
    }
}
