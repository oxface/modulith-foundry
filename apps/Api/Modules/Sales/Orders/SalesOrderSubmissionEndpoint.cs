using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderSubmissionEndpoint
{
    internal static async Task<IResult> HandleAsync(
        long orderNumber,
        SubmitRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderOperations operations,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        SubmitSalesOrderResult result = await operations.SubmitAsync(
            new(context.UserId, context.OrganizationId, orderNumber, request.ExpectedVersion),
            cancellationToken
        );
        return result switch
        {
            SubmitSalesOrderResult.Submitted submitted => TypedResults.Ok(
                SalesOrderResponses.ToResponse(submitted.Order)
            ),
            SubmitSalesOrderResult.InvalidExpectedVersion => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid order version",
                detail: "Expected version must be positive.",
                extensions: new Dictionary<string, object?> { ["field"] = "expectedVersion" }
            ),
            SubmitSalesOrderResult.NotFound => Results.NotFound(),
            SubmitSalesOrderResult.NotDraft => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order is not a draft",
                detail: "Only a draft sales order can be submitted."
            ),
            SubmitSalesOrderResult.VersionConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order version conflict",
                detail: "The order has changed. Reload it before retrying."
            ),
            SubmitSalesOrderResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    internal sealed record SubmitRequest(long ExpectedVersion);
}
