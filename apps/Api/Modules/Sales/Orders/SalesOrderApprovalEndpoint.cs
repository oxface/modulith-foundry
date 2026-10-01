using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderApprovalEndpoint
{
    internal static async Task<IResult> HandleAsync(
        long orderNumber,
        ApprovalRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderApproval approval,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        ApproveSalesOrderResult result = await approval.ApproveAsync(
            new(context.UserId, context.OrganizationId, orderNumber, request.ExpectedVersion),
            cancellationToken
        );
        return result switch
        {
            ApproveSalesOrderResult.Approved approved => TypedResults.Ok(
                SalesOrderResponses.ToResponse(approved.Order)
            ),
            ApproveSalesOrderResult.InvalidExpectedVersion => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid order version",
                detail: "Expected version must be positive.",
                extensions: new Dictionary<string, object?> { ["field"] = "expectedVersion" }
            ),
            ApproveSalesOrderResult.NotFound => Results.NotFound(),
            ApproveSalesOrderResult.NotAwaitingApproval => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order is not awaiting approval"
            ),
            ApproveSalesOrderResult.VersionConflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Order version conflict",
                detail: "The order has changed. Reload it before retrying."
            ),
            ApproveSalesOrderResult.PermissionDenied => Results.Forbid(),
            ApproveSalesOrderResult.SelfApprovalDenied => Denied(
                "Self approval prohibited",
                "The submitter cannot approve the same order."
            ),
            ApproveSalesOrderResult.AuthorityUnavailable => Denied(
                "Approval authority unavailable",
                "An enabled authority for the current membership is required."
            ),
            ApproveSalesOrderResult.CurrencyMismatch => Denied(
                "Approval currency mismatch",
                "The authority does not cover the order currency."
            ),
            ApproveSalesOrderResult.LimitExceeded => Denied(
                "Approval limit exceeded",
                "The authority does not cover the order amount."
            ),
            _ => throw new UnreachableException(),
        };
    }

    private static IResult Denied(string title, string detail) =>
        Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: title, detail: detail);

    internal sealed record ApprovalRequest(long ExpectedVersion);
}
