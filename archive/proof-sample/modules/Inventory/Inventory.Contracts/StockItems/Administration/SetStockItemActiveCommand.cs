using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record SetStockItemActiveCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Sku,
    bool IsActive
);
