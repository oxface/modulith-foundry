namespace ModulithFoundry.Modules.Sales.Contracts;

public interface ISalesOrderCancellation
{
    Task<CancelSalesOrderResult> CancelAsync(
        CancelSalesOrderCommand command,
        CancellationToken cancellationToken = default
    );
}
