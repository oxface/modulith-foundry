using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderCancellationEndpoint
{
    internal static async Task<IResult> HandleAsync(
        long orderNumber,
        CancelRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderCancellation cancellation,
        CancellationToken cancellationToken
    )
    {
        var context = contextAccessor.GetRequiredOrganizationContext();
        var result = await cancellation.CancelAsync(
            new(
                context.UserId,
                context.OrganizationId,
                orderNumber,
                request.ExpectedVersion,
                request.Reason ?? ""
            ),
            cancellationToken
        );
        return result switch
        {
            CancelSalesOrderResult.Cancelled cancelled => TypedResults.Ok(
                SalesOrderResponses.ToResponse(cancelled.Order)
            ),
            CancelSalesOrderResult.Unchanged unchanged => TypedResults.Ok(
                SalesOrderResponses.ToResponse(unchanged.Order)
            ),
            CancelSalesOrderResult.InvalidExpectedVersion => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid order version",
                detail: "Expected version must be positive.",
                extensions: new Dictionary<string, object?> { ["field"] = "expectedVersion" }
            ),
            CancelSalesOrderResult.InvalidReason => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid cancellation reason",
                detail: "A reason must contain 1–500 printable characters.",
                extensions: new Dictionary<string, object?> { ["field"] = "reason" }
            ),
            CancelSalesOrderResult.NotFound => Results.NotFound(),
            CancelSalesOrderResult.VersionConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order version conflict",
                detail: "The order or its fulfilment process has changed. Reload it before retrying."
            ),
            CancelSalesOrderResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    internal sealed record CancelRequest(long ExpectedVersion, string? Reason);
}
