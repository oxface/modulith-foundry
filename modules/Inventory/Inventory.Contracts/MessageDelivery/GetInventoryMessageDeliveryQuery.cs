using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record GetInventoryMessageDeliveryQuery(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    Guid MessageId
);
