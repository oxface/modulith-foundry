using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record SetStockingLocationActiveCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Code,
    bool IsActive
);
