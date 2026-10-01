namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ISalesOrderApproval
{
    Task<ApproveSalesOrderResult> ApproveAsync(
        ApproveSalesOrderCommand command,
        CancellationToken cancellationToken = default
    );
}
