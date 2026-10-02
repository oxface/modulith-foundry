using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;

namespace ModulithFoundry.Modules.Inventory.Messaging.Recovery;

internal static class InventoryMessageDeliveryMappings
{
    internal static InventoryMessageDeliveryView ToView(this InventoryOutboxMessage message) =>
        new(
            message.MessageId,
            message.MessageType,
            message.CreatedAt,
            message.AvailableAt,
            message.DispatchedAt,
            message.LeaseUntil,
            message.Attempts
        );
}
