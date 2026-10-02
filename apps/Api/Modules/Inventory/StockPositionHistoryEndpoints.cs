using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Api.Modules.Inventory;

internal static class StockPositionHistoryEndpoints
{
    internal static IEndpointRouteBuilder MapStockPositionHistoryEndpoints(
        this IEndpointRouteBuilder endpoints
    )
    {
        endpoints.MapGet("/{locationCode}/{sku}/history", GetAsync);
        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        string locationCode,
        string sku,
        long? afterVersion,
        int? limit,
        IOrganizationContextAccessor contextAccessor,
        IStockPositionOperations stockPositions,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetStockPositionHistoryResult result = await stockPositions.GetHistoryAsync(
            new(
                context.UserId,
                context.OrganizationId,
                locationCode,
                sku,
                afterVersion ?? 0,
                limit ?? 50
            ),
            cancellationToken
        );
        return result switch
        {
            GetStockPositionHistoryResult.Found found => TypedResults.Ok(ToResponse(found.History)),
            GetStockPositionHistoryResult.NotFound => Results.NotFound(),
            GetStockPositionHistoryResult.PermissionDenied => Results.Forbid(),
            GetStockPositionHistoryResult.Invalid invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid stock position history query",
                detail: invalid.Detail,
                extensions: new Dictionary<string, object?> { ["field"] = invalid.Field }
            ),
            _ => throw new UnreachableException(),
        };
    }

    private static HistoryResponse ToResponse(StockPositionHistoryView history) =>
        new(
            history.StockPositionId.Value,
            history.BaseUnitCode,
            history.Version,
            [
                .. history.Entries.Select(entry => new HistoryEntryResponse(
                    entry.Version,
                    entry.RecordedAt,
                    entry.Action switch
                    {
                        StockPositionHistoryAction.Opened => "opened",
                        StockPositionHistoryAction.Received => "received",
                        StockPositionHistoryAction.QuantityCorrected => "quantity-corrected",
                        StockPositionHistoryAction.Reserved => "reserved",
                        StockPositionHistoryAction.ReservationReleased => "reservation-released",
                        _ => throw new UnreachableException(),
                    },
                    entry.Quantity,
                    entry.Reason
                )),
            ],
            history.NextAfterVersion
        );

    private sealed record HistoryResponse(
        Guid StockPositionId,
        string BaseUnitCode,
        long Version,
        IReadOnlyList<HistoryEntryResponse> Entries,
        long? NextAfterVersion
    );

    private sealed record HistoryEntryResponse(
        long Version,
        DateTimeOffset RecordedAt,
        string Action,
        decimal? Quantity,
        string? Reason
    );
}
