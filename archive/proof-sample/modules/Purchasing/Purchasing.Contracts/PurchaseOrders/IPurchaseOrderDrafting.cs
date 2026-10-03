namespace ModulithFoundry.Modules.Purchasing.Contracts;

public interface IPurchaseOrderDrafting
{
    Task<CreatePurchaseOrderResult> CreateAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default
    );
    Task<SetPurchaseOrderLineResult> SetLineAsync(
        SetPurchaseOrderLineCommand command,
        CancellationToken cancellationToken = default
    );
}
