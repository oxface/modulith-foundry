using ModulithFoundry.Modules.Purchasing.Contracts;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.CreatePurchaseOrder;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.SetPurchaseOrderLine;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders;

internal sealed class PurchaseOrderDrafting(
    CreatePurchaseOrderHandler create,
    SetPurchaseOrderLineHandler setLine
) : IPurchaseOrderDrafting
{
    public Task<CreatePurchaseOrderResult> CreateAsync(
        CreatePurchaseOrderCommand command,
        CancellationToken cancellationToken = default
    ) => create.HandleAsync(command, cancellationToken);

    public Task<SetPurchaseOrderLineResult> SetLineAsync(
        SetPurchaseOrderLineCommand command,
        CancellationToken cancellationToken = default
    ) => setLine.HandleAsync(command, cancellationToken);
}
