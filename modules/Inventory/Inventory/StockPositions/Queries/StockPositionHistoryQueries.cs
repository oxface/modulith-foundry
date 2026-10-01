using System.Diagnostics;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.StockPositions.Events;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Queries;

internal sealed class StockPositionHistoryQueries(
    StockPositionQueries currentQueries,
    StockPositionEventReader eventReader
)
{
    internal async Task<GetStockPositionHistoryResult> GetAsync(
        GetStockPositionHistoryQuery query,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(query);
        GetStockPositionResult current = await currentQueries.GetCurrentAsync(
            new(query.ActorUserId, query.OrganizationId, query.StockingLocationCode, query.Sku),
            cancellationToken
        );
        if (current is not GetStockPositionResult.Found found)
        {
            return current switch
            {
                GetStockPositionResult.PermissionDenied =>
                    new GetStockPositionHistoryResult.PermissionDenied(),
                GetStockPositionResult.NotFound => new GetStockPositionHistoryResult.NotFound(),
                GetStockPositionResult.Invalid invalid => new GetStockPositionHistoryResult.Invalid(
                    invalid.Field,
                    invalid.Detail
                ),
                _ => throw new UnreachableException(),
            };
        }

        if (query.AfterVersion < 0)
        {
            return new GetStockPositionHistoryResult.Invalid(
                "afterVersion",
                "After-version cannot be negative."
            );
        }

        if (query.Limit is < 1 or > GetStockPositionHistoryQuery.MaximumPageSize)
        {
            return new GetStockPositionHistoryResult.Invalid(
                "limit",
                $"Page size must be between 1 and {GetStockPositionHistoryQuery.MaximumPageSize}."
            );
        }

        StockPositionEventPage? page = await eventReader.ReadPageAsync(
            found.Position.StockPositionId.Value,
            query.AfterVersion,
            query.Limit,
            cancellationToken
        );
        if (page is null)
        {
            return new GetStockPositionHistoryResult.NotFound();
        }
        StockPositionHistoryEntry[] entries = [.. page.Events.Select(ToEntry)];
        return new GetStockPositionHistoryResult.Found(
            new StockPositionHistoryView(
                found.Position.StockPositionId,
                found.Position.BaseUnitCode,
                page.Version,
                entries,
                page.NextAfterVersion
            )
        );
    }

    private static StockPositionHistoryEntry ToEntry(StoredEvent stored)
    {
        IStockPositionEvent @event = StockPositionEventSerializer.Deserialize(stored);
        return @event switch
        {
            StockPositionOpened => new(
                stored.StreamVersion,
                stored.RecordedAt,
                StockPositionHistoryAction.Opened,
                null
            ),
            StockReceived received => new(
                stored.StreamVersion,
                stored.RecordedAt,
                StockPositionHistoryAction.Received,
                received.Quantity
            ),
            StockQuantityCorrected corrected => new(
                stored.StreamVersion,
                stored.RecordedAt,
                StockPositionHistoryAction.QuantityCorrected,
                corrected.OnHandQuantity,
                corrected.Reason
            ),
            _ => throw new StockPositionIntegrityException(
                stored.StreamId,
                StockPositionIntegrityFailure.UnknownEvent,
                observedVersion: stored.StreamVersion
            ),
        };
    }
}
