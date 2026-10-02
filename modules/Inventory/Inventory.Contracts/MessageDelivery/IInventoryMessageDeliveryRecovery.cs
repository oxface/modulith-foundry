namespace ModulithFoundry.Modules.Inventory.Contracts;

// Authorized operator capability. Deliberately not mapped to a public HTTP endpoint.
public interface IInventoryMessageDeliveryRecovery
{
    Task<GetInventoryMessageDeliveryResult> GetAsync(
        GetInventoryMessageDeliveryQuery query,
        CancellationToken cancellationToken = default
    );

    Task<RequeueInventoryMessageResult> RequeueAsync(
        RequeueInventoryMessageCommand command,
        CancellationToken cancellationToken = default
    );
}
