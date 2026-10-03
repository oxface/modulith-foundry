using ModulithFoundry.ExecutionIdentity;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;

namespace ModulithFoundry.Samples.Wholesale.ContextDemo.Sales;

internal sealed class DraftOrderPreview(IOperationContextAccessor context, IStockAvailability stock)
    : IDraftOrderPreview
{
    public DraftPreview Preview(string sku, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        OperationContext current = context.Current;
        Actor actor = current.RequireTenantAndIdentifiedActor().Actor;
        int available = stock.GetAvailableQuantity(sku);
        return new DraftPreview(
            sku,
            quantity,
            available,
            quantity <= available,
            actor,
            current.Initiator
        );
    }
}
