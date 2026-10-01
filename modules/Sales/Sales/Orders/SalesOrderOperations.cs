using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Fulfilment.Queries;
using ModulithFoundry.Modules.Sales.Orders.CreateDraftSalesOrder;
using ModulithFoundry.Modules.Sales.Orders.Queries;
using ModulithFoundry.Modules.Sales.Orders.SubmitSalesOrder;

namespace ModulithFoundry.Modules.Sales.Orders;

internal sealed class SalesOrderOperations(
    CreateDraftSalesOrderHandler create,
    SalesOrderQueries queries,
    SubmitSalesOrderHandler submit,
    OrderFulfilmentQueries fulfilment
) : ISalesOrderOperations
{
    public Task<GetOrderFulfilmentResult> GetFulfilmentAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken = default
    ) => fulfilment.GetAsync(actorUserId, organizationId, orderNumber, cancellationToken);

    public Task<SubmitSalesOrderResult> SubmitAsync(
        SubmitSalesOrderCommand command,
        CancellationToken cancellationToken = default
    ) => submit.HandleAsync(command, cancellationToken);

    public Task<GetSalesOrderActivityResult> GetActivityAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken = default
    ) => queries.GetActivityAsync(actorUserId, organizationId, orderNumber, cancellationToken);

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
