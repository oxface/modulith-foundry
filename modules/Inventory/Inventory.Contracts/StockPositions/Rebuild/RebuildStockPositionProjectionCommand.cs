using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record RebuildStockPositionProjectionCommand(UserId ActorUserId, OrganizationId OrganizationId, StockPositionId StockPositionId);
