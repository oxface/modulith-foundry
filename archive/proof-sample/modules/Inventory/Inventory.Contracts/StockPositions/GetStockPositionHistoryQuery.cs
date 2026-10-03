using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record GetStockPositionHistoryQuery(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string StockingLocationCode,
    string Sku,
    long AfterVersion = 0,
    int Limit = 50
)
{
    public const int MaximumPageSize = 100;
}
