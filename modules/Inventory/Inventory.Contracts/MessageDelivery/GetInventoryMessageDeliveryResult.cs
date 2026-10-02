namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record GetInventoryMessageDeliveryResult
{
    private GetInventoryMessageDeliveryResult() { }

    public sealed record Found(InventoryMessageDeliveryView Delivery)
        : GetInventoryMessageDeliveryResult;

    public sealed record NotFound : GetInventoryMessageDeliveryResult;

    public sealed record PermissionDenied : GetInventoryMessageDeliveryResult;
}
