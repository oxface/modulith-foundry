using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record RequeueInventoryMessageCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    Guid MessageId,
    int ExpectedDispatchAttempts,
    string Reason
);
