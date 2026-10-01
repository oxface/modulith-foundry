using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record ChangeStockItemDescriptionCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string Sku,
    string Description
);
