using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record RenameStockingLocationCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Code,
    string Name
);
