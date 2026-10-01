using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ISalesOrderOperations
{
    Task<CreateDraftSalesOrderResult> CreateDraftAsync(CreateDraftSalesOrderCommand command, CancellationToken cancellationToken = default);
    Task<GetSalesOrderResult> GetByNumberAsync(UserId actorUserId, OrganizationId organizationId, long orderNumber, CancellationToken cancellationToken = default);
}
