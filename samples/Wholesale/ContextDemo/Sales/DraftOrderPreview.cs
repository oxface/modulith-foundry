using ModulithFoundry.Samples.Wholesale.ContextDemo.Inventory.Contracts;
using ModulithFoundry.Samples.Wholesale.ContextDemo.Sales.Contracts;
using Rootbolt.ActorIdentity;
using Rootbolt.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.ContextDemo.Sales;

internal sealed class DraftOrderPreview(
    IActorContextAccessor actors,
    ITenantContextAccessor tenancy,
    IStockAvailability stock
) : IDraftOrderPreview
{
    public DraftPreview Preview(string sku, int quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        tenancy.Current.RequireTenant();
        ActorContext current = actors.Current;
        Actor actor = current.RequireIdentifiedActor();
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
