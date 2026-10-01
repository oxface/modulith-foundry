using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record RecordStockReceiptCommand(
    UserId ActorUserId,
    OrganizationId OrganizationId,
    string StockingLocationCode,
    string Sku,
    decimal Quantity,
    long ExpectedVersion
);
