using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders.CreateDraftSalesOrder;
using ModulithFoundry.Modules.Sales.Orders.Queries;

namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed class SalesOrderOperations(
    CreateDraftSalesOrderHandler create,
    SalesOrderQueries queries
) : ISalesOrderOperations
{
    public Task<CreateDraftSalesOrderResult> CreateDraftAsync(
        CreateDraftSalesOrderCommand command,
        CancellationToken cancellationToken = default
    ) => create.HandleAsync(command, cancellationToken);

    public Task<GetSalesOrderResult> GetByNumberAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken = default
    ) => queries.GetByNumberAsync(actorUserId, organizationId, orderNumber, cancellationToken);
}
