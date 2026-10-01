using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record GetStockPositionAsOfQuery(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string StockingLocationCode,
    string Sku,
    DateTimeOffset RecordedAt
);
