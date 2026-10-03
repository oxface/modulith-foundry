using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class OrderFulfilmentEndpoint
{
    internal static async Task<IResult> HandleAsync(
        long orderNumber,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderOperations operations,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetOrderFulfilmentResult result = await operations.GetFulfilmentAsync(
            context.UserId,
            context.OrganizationId,
            orderNumber,
            cancellationToken
        );
        return result switch
        {
            GetOrderFulfilmentResult.Found found => TypedResults.Ok(
                new FulfilmentResponse(
                    found.Process.ProcessId,
                    found.Process.OrderNumber,
                    OrderFulfilmentStatusValues.ToValue(found.Process.Status),
                    found.Process.CreatedAt,
                    found.Process.Version,
                    found.Process.StockingLocationId,
                    [
                        .. found.Process.Lines.Select(line => new LineResponse(
                            line.LineNumber,
                            line.StockItemId.Value,
                            line.Quantity,
                            line.BaseUnitCode,
                            OrderFulfilmentLineStatusValues.ToValue(line.Status),
                            line.ReservationId,
                            line.AvailableQuantity,
                            line.ReasonCode,
                            line.AttemptCount,
                            line.ResponseDeadline,
                            line.ReplenishmentQuantity,
                            line.ReplenishmentRequirementId,
                            line.ReplenishmentRequirementNumber,
                            line.ReplenishmentReasonCode,
                            line.ReleaseStatus.HasValue
                                ? OrderFulfilmentReleaseStatusValues.ToValue(
                                    line.ReleaseStatus.Value
                                )
                                : null,
                            line.ReleaseReasonCode,
                            line.ReleaseResponseDeadline
                        )),
                    ]
                )
            ),
            GetOrderFulfilmentResult.NotFound => Results.NotFound(),
            GetOrderFulfilmentResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private sealed record FulfilmentResponse(
        Guid ProcessId,
        long OrderNumber,
        string Status,
        DateTimeOffset CreatedAt,
        long Version,
        Guid? StockingLocationId,
        IReadOnlyList<LineResponse> Lines
    );

    private sealed record LineResponse(
        int LineNumber,
        Guid StockItemId,
        decimal Quantity,
        string BaseUnitCode,
        string Status,
        Guid? ReservationId,
        decimal? AvailableQuantity,
        string? ReasonCode,
        int AttemptCount,
        DateTimeOffset? ResponseDeadline,
        decimal? ReplenishmentQuantity,
        Guid? ReplenishmentRequirementId,
        long? ReplenishmentRequirementNumber,
        string? ReplenishmentReasonCode,
        string? ReleaseStatus,
        string? ReleaseReasonCode,
        DateTimeOffset? ReleaseResponseDeadline
    );
}
