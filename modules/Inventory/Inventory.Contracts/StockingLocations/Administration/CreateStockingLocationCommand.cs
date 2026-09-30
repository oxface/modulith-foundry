using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record CreateStockingLocationCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Code,
    string Name);
