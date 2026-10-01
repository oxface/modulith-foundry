using System.Diagnostics;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderActivityEndpoint
{
    internal static async Task<IResult> HandleAsync(
        long orderNumber,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderOperations operations,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetSalesOrderActivityResult result = await operations.GetActivityAsync(
            context.UserId,
            context.OrganizationId,
            orderNumber,
            cancellationToken
        );
        return result switch
        {
            GetSalesOrderActivityResult.Found found => TypedResults.Ok(
                found
                    .Entries.Select(entry => new ActivityResponse(
                        SalesOrderActivityKindValues.ToValue(entry.Kind),
                        entry.ActorUserId.Value,
                        entry.OrderVersion,
                        entry.OccurredAt
                    ))
                    .ToArray()
            ),
            GetSalesOrderActivityResult.NotFound => Results.NotFound(),
            GetSalesOrderActivityResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private sealed record ActivityResponse(
        string Kind,
        Guid ActorUserId,
        long OrderVersion,
        DateTimeOffset OccurredAt
    );
}
