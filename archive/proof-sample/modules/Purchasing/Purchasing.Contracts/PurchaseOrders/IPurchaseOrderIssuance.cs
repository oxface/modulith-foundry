namespace ModulithFoundry.Modules.Purchasing.Contracts;

public interface IPurchaseOrderIssuance
{
    Task<IssuePurchaseOrderResult> IssueAsync(
        IssuePurchaseOrderCommand command,
        CancellationToken cancellationToken = default
    );
}
