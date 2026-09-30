using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Queries;

internal sealed class StockPositionTemporalQueries(
    StockPositionQueries currentQueries,
    StockPositionEventReader eventReader)
{
    internal async Task<GetStockPositionResult> GetAtVersionAsync(
        GetStockPositionAtVersionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        GetStockPositionResult current = await currentQueries.GetCurrentAsync(
            new(query.ActorUserId, query.OrganizationId, query.StockingLocationCode, query.Sku), cancellationToken);
        if (current is not GetStockPositionResult.Found found) { return current; }
        if (query.Version < 1)
        {
            return new GetStockPositionResult.Invalid("version", "Version must be greater than zero.");
        }

        StockPositionAggregate? aggregate = await eventReader.LoadAtVersionAsync(
            found.Position.StockPositionId.Value, query.Version, cancellationToken);
        return ToResult(found.Position, aggregate);
    }

    internal async Task<GetStockPositionResult> GetAsOfAsync(
        GetStockPositionAsOfQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        GetStockPositionResult current = await currentQueries.GetCurrentAsync(
            new(query.ActorUserId, query.OrganizationId, query.StockingLocationCode, query.Sku), cancellationToken);
        if (current is not GetStockPositionResult.Found found) { return current; }
        StockPositionAggregate? aggregate = await eventReader.LoadAsOfAsync(
            found.Position.StockPositionId.Value, query.RecordedAt, cancellationToken);
        return ToResult(found.Position, aggregate);
    }

    private static GetStockPositionResult ToResult(StockPositionView current, StockPositionAggregate? aggregate)
    {
        if (aggregate?.State is not { } state) { return new GetStockPositionResult.NotFound(); }
        if (state.StockItemId != current.StockItemId.Value || state.StockingLocationId != current.StockingLocationId.Value
            || !string.Equals(state.BaseUnitCode, current.BaseUnitCode, StringComparison.Ordinal))
        {
            throw new StockPositionIntegrityException(
                aggregate.StreamId, StockPositionIntegrityFailure.WriteModelIdentityMismatch,
                observedVersion: aggregate.Version);
        }

        return new GetStockPositionResult.Found(current with
        {
            OnHandQuantity = state.OnHand.Value,
            ReservedQuantity = state.Reserved.Value,
            AvailableQuantity = state.Available.Value,
            Version = aggregate.Version,
        });
    }
}
