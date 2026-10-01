using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record CorrectStockQuantityCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string StockingLocationCode,
    string Sku,
    decimal OnHandQuantity,
    string Reason,
    long ExpectedVersion);
