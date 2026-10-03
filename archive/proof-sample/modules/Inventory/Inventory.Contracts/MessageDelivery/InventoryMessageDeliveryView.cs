namespace ModulithFoundry.Modules.Inventory.Contracts;

// Publication metadata, not proof of subscriber processing. Contains no retained payload.
public sealed record InventoryMessageDeliveryView(
    Guid MessageId,
    string MessageType,
    DateTimeOffset CreatedAt,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? LastPublishedAt,
    DateTimeOffset? LeaseExpiresAt,
    int DispatchAttempts
);
