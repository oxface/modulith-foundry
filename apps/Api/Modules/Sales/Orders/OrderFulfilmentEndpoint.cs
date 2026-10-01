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
                    found.Process.CreatedAt
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
        DateTimeOffset CreatedAt
    );
}
