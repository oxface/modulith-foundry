namespace ModulithFoundry.Modules.Inventory.Contracts;

public abstract record RequeueInventoryMessageResult
{
    private RequeueInventoryMessageResult() { }

    public sealed record Requeued(InventoryMessageDeliveryView Delivery)
        : RequeueInventoryMessageResult;

    public sealed record AlreadyQueued(InventoryMessageDeliveryView Delivery)
        : RequeueInventoryMessageResult;

    public sealed record NotFound : RequeueInventoryMessageResult;

    public sealed record PermissionDenied : RequeueInventoryMessageResult;

    public sealed record InvalidExpectedDispatchAttempts : RequeueInventoryMessageResult;

    public sealed record InvalidReason : RequeueInventoryMessageResult;

    public sealed record InFlight : RequeueInventoryMessageResult;

    public sealed record VersionConflict : RequeueInventoryMessageResult;

    public sealed record UnsupportedMessageType : RequeueInventoryMessageResult;
}
