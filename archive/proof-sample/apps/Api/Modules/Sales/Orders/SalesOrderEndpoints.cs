using System.Diagnostics;
using System.Globalization;
using ModulithFoundry.Api.Authentication;
using ModulithFoundry.Api.Modules.Access.Middleware;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;

namespace ModulithFoundry.Api.Modules.Sales.Orders;

internal static class SalesOrderEndpoints
{
    internal static RouteGroupBuilder MapSalesOrderEndpoints(this RouteGroupBuilder sales)
    {
        RouteGroupBuilder orders = sales.MapGroup("/orders");
        orders.MapPost("", CreateDraftAsync).RequireBffAntiforgery();
        orders.MapGet("/{orderNumber:long}", GetAsync);
        orders
            .MapPost("/{orderNumber:long}/submit", SalesOrderSubmissionEndpoint.HandleAsync)
            .RequireBffAntiforgery();
        orders.MapGet("/{orderNumber:long}/activity", SalesOrderActivityEndpoint.HandleAsync);
        orders
            .MapPost("/{orderNumber:long}/approve", SalesOrderApprovalEndpoint.HandleAsync)
            .RequireBffAntiforgery();
        orders.MapGet("/{orderNumber:long}/fulfilment", OrderFulfilmentEndpoint.HandleAsync);
        orders
            .MapPost("/{orderNumber:long}/cancel", SalesOrderCancellationEndpoint.HandleAsync)
            .RequireBffAntiforgery();
        return sales;
    }

    private static async Task<IResult> CreateDraftAsync(
        CreateDraftRequest request,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderOperations operations,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        DraftSalesOrderLine[]? lines = request
            .Lines?.Select(line =>
                line is null
                    ? null!
                    : new DraftSalesOrderLine(
                        new StockItemId(line.StockItemId),
                        line.Quantity,
                        line.UnitPrice
                    )
            )
            .ToArray();
        CreateDraftSalesOrderResult result = await operations.CreateDraftAsync(
            new(
                context.UserId,
                context.OrganizationId,
                request.CustomerCode,
                request.Currency,
                lines!
            ),
            cancellationToken
        );
        return result switch
        {
            CreateDraftSalesOrderResult.Created created => TypedResults.Created(
                $"/api/o/{Uri.EscapeDataString(context.OrganizationSlug)}/sales/orders/{created.Order.OrderNumber.ToString(CultureInfo.InvariantCulture)}",
                SalesOrderResponses.ToResponse(created.Order)
            ),
            CreateDraftSalesOrderResult.Invalid invalid => Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid sales order",
                detail: invalid.Detail,
                extensions: new Dictionary<string, object?> { ["field"] = invalid.Field }
            ),
            CreateDraftSalesOrderResult.CustomerNotFound => Results.NotFound(),
            CreateDraftSalesOrderResult.ItemsUnavailable unavailable => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Stock items unavailable",
                detail: "Some requested stock items are missing or inactive.",
                extensions: new Dictionary<string, object?>
                {
                    ["missingItemIds"] = unavailable
                        .MissingItemIds.Select(id => id.Value)
                        .ToArray(),
                    ["inactiveItemIds"] = unavailable
                        .InactiveItemIds.Select(id => id.Value)
                        .ToArray(),
                }
            ),
            CreateDraftSalesOrderResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private static async Task<IResult> GetAsync(
        long orderNumber,
        IOrganizationContextAccessor contextAccessor,
        ISalesOrderOperations operations,
        CancellationToken cancellationToken
    )
    {
        OrganizationAccessContext context = contextAccessor.GetRequiredOrganizationContext();
        GetSalesOrderResult result = await operations.GetByNumberAsync(
            context.UserId,
            context.OrganizationId,
            orderNumber,
            cancellationToken
        );
        return result switch
        {
            GetSalesOrderResult.Found found => TypedResults.Ok(
                SalesOrderResponses.ToResponse(found.Order)
            ),
            GetSalesOrderResult.NotFound => Results.NotFound(),
            GetSalesOrderResult.PermissionDenied => Results.Forbid(),
            _ => throw new UnreachableException(),
        };
    }

    private sealed record CreateDraftRequest(
        string CustomerCode,
        string Currency,
        IReadOnlyList<CreateDraftLineRequest?>? Lines
    );

    private sealed record CreateDraftLineRequest(
        Guid StockItemId,
        decimal Quantity,
        decimal UnitPrice
    );
}
