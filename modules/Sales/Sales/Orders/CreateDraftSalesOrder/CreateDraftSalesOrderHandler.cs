using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Sales.Audit;
using ModulithFoundry.Modules.Sales.Authorization;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Customers;
using ModulithFoundry.Modules.Sales.Orders.Persistence;
using ModulithFoundry.Modules.Sales.Persistence;

namespace ModulithFoundry.Modules.Sales.Orders.CreateDraftSalesOrder;

internal sealed class CreateDraftSalesOrderHandler(
    SalesDbContext context,
    SalesRequestAuthorization authorization,
    IStockItemReferences references,
    SalesOrderNumberAllocator numbers,
    TimeProvider timeProvider
)
{
    internal async Task<CreateDraftSalesOrderResult> HandleAsync(
        CreateDraftSalesOrderCommand command,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!authorization.MatchesContext(command.ActorUserId, command.OrganizationId))
        {
            return new CreateDraftSalesOrderResult.PermissionDenied();
        }
        if (
            !await authorization.HasPermissionAsync(
                command.ActorUserId,
                command.OrganizationId,
                SalesPermissionIds.OrdersCreate,
                cancellationToken
            )
        )
        {
            context.AuditEntries.Add(
                SalesAuditEntry.PermissionDenied(
                    command.OrganizationId.Value,
                    command.ActorUserId.Value,
                    SalesAuditActions.OrderCreateDenied,
                    SalesAuditSubjectTypes.SalesOrder,
                    timeProvider.GetUtcNow()
                )
            );
            await context.SaveChangesAsync(cancellationToken);
            return new CreateDraftSalesOrderResult.PermissionDenied();
        }

        string customerCode;
        try
        {
            customerCode = CustomerInput.NormalizeCode(command.CustomerCode);
        }
        catch (InvalidCustomerInputException exception)
        {
            return new CreateDraftSalesOrderResult.Invalid("customerCode", exception.Message);
        }

        Guid? customerId = await context
            .Customers.AsNoTracking()
            .Where(customer =>
                customer.OrganizationId == command.OrganizationId.Value
                && customer.Code == customerCode
            )
            .Select(customer => (Guid?)customer.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (customerId is null)
        {
            return new CreateDraftSalesOrderResult.CustomerNotFound();
        }

        if (
            command.Lines is null
            || command.Lines.Count is < 1 or > 100
            || command.Lines.Any(line => line is null)
        )
        {
            return new CreateDraftSalesOrderResult.Invalid(
                "lines",
                "An order must contain 1–100 non-null lines."
            );
        }

        DraftSalesOrderLine[] inputs = [.. command.Lines];
        StockItemReferenceResolution resolution = await references.ResolveAsync(
            command.OrganizationId,
            inputs.Select(line => line.StockItemId).Distinct().ToArray(),
            cancellationToken
        );
        if (resolution.MissingItemIds.Count > 0 || resolution.InactiveItemIds.Count > 0)
        {
            return new CreateDraftSalesOrderResult.ItemsUnavailable(
                resolution.MissingItemIds,
                resolution.InactiveItemIds
            );
        }
        Dictionary<StockItemId, StockItemReference> byId = resolution.Items.ToDictionary(item =>
            item.StockItemId
        );
        SalesOrderLineInput[] snapshots =
        [
            .. inputs.Select(line =>
            {
                StockItemReference item = byId[line.StockItemId];
                return new SalesOrderLineInput(
                    item.StockItemId.Value,
                    item.Sku,
                    item.Description,
                    item.BaseUnitCode,
                    line.Quantity,
                    line.UnitPrice
                );
            }),
        ];
        DateTimeOffset now = timeProvider.GetUtcNow();
        long number = await numbers.AllocateAsync(command.OrganizationId.Value, cancellationToken);
        SalesOrder order;
        try
        {
            order = SalesOrder.CreateDraft(
                Guid.CreateVersion7(now),
                command.OrganizationId.Value,
                customerId.Value,
                number,
                command.Currency,
                snapshots,
                now
            );
        }
        catch (InvalidSalesOrderInputException exception)
        {
            return new CreateDraftSalesOrderResult.Invalid(exception.Field, exception.Message);
        }

        context.SalesOrders.Add(order);
        context.AuditEntries.Add(
            SalesAuditEntry.Succeeded(
                command.OrganizationId.Value,
                command.ActorUserId.Value,
                SalesAuditActions.OrderCreated,
                SalesAuditSubjectTypes.SalesOrder,
                order.Id,
                new
                {
                    order.OrderNumber,
                    order.Currency,
                    order.TotalAmount,
                    LineCount = order.Lines.Count,
                },
                now
            )
        );
        await context.SaveChangesAsync(cancellationToken);
        return new CreateDraftSalesOrderResult.Created(order.ToView());
    }
}
