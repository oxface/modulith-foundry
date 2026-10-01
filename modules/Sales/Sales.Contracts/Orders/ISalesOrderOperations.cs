using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ISalesOrderOperations
{
    Task<SubmitSalesOrderResult> SubmitAsync(
        SubmitSalesOrderCommand command,
        CancellationToken cancellationToken = default
    );
    Task<GetSalesOrderActivityResult> GetActivityAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken = default
    );
    Task<CreateDraftSalesOrderResult> CreateDraftAsync(
        CreateDraftSalesOrderCommand command,
        CancellationToken cancellationToken = default
    );
    Task<GetSalesOrderResult> GetByNumberAsync(
        UserId actorUserId,
        OrganizationId organizationId,
        long orderNumber,
        CancellationToken cancellationToken = default
    );
}
