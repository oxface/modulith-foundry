using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record GetStockPositionAtVersionQuery(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string StockingLocationCode,
    string Sku,
    long Version
);
