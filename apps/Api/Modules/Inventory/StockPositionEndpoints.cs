using System.Diagnostics;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Api.Modules.Inventory;

internal static class StockPositionEndpoints
{
    internal static IEndpointRouteBuilder MapStockPositionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder positions = endpoints.MapGroup("/stock-positions");
        positions.MapGet("/{locationCode}/{sku}", GetCurrentAsync);
        positions.MapPost("/{locationCode}/{sku}/receipts", RecordReceiptAsync)
            .RequireBffAntiforgery();
        return endpoints;
    }

    private static async Task<IResult> GetCurrentAsync(
        string locationCode,
        string sku,
        IOrganizationContextAccessor contextAccessor,
        IStockPositions stockPositions,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetStockPositionResult result = await stockPositions.GetCurrentAsync(
            new GetStockPositionQuery(
                context.UserId,
                context.OrganizationId,
                locationCode,
                sku),
            cancellationToken);
        return result switch
        {
            GetStockPositionResult.Found found => TypedResults.Ok(ToResponse(found.Position)),
            GetStockPositionResult.Invalid invalid => InvalidStockPosition(
                invalid.Field,
                invalid.Detail),
            GetStockPositionResult.NotFound => Results.NotFound(),
            GetStockPositionResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> RecordReceiptAsync(
        string locationCode,
        string sku,
        RecordStockReceiptRequest request,
        IOrganizationContextAccessor contextAccessor,
        IStockPositions stockPositions,
        CancellationToken cancellationToken)
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        RecordStockReceiptResult result = await stockPositions.RecordReceiptAsync(
            new RecordStockReceiptCommand(
                context.UserId,
                context.OrganizationId,
                locationCode,
                sku,
                request.Quantity,
                request.ExpectedVersion),
            cancellationToken);
        return result switch
        {
            RecordStockReceiptResult.Recorded recorded =>
                TypedResults.Ok(ToResponse(recorded.Position)),
            RecordStockReceiptResult.Invalid invalid => InvalidStockPosition(
                invalid.Field,
                invalid.Detail),
            RecordStockReceiptResult.ReferenceUnavailable unavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Inventory reference unavailable",
                detail: $"The required {unavailable.Reference} is missing or inactive."),
            RecordStockReceiptResult.VersionConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Stock position version conflict",
                detail: "The Stock Position changed after it was read."),
            RecordStockReceiptResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static IResult InvalidStockPosition(string field, string detail) =>
        Results.Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid stock position operation",
            detail: detail,
            extensions: new Dictionary<string, object?> { ["field"] = field });

    private static StockPositionResponse ToResponse(StockPositionView position) =>
        new(
            position.StockPositionId.Value,
            position.StockItemId.Value,
            position.StockingLocationId.Value,
            position.Sku,
            position.StockingLocationCode,
            position.BaseUnitCode,
            position.OnHandQuantity,
            position.ReservedQuantity,
            position.AvailableQuantity,
            position.Version);

    private sealed record RecordStockReceiptRequest(decimal Quantity, long ExpectedVersion);

    private sealed record StockPositionResponse(
        Guid StockPositionId,
        Guid StockItemId,
        Guid StockingLocationId,
        string Sku,
        string StockingLocationCode,
        string BaseUnitCode,
        decimal OnHandQuantity,
        decimal ReservedQuantity,
        decimal AvailableQuantity,
        long Version);
}
