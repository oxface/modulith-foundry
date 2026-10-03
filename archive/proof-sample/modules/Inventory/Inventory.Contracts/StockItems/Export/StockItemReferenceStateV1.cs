namespace ModulithFoundry.Modules.Inventory.Contracts;

public sealed record StockItemReferenceStateV1(
    Guid OrganizationId,
    Guid StockItemId,
    string Sku,
    string Description,
    string BaseUnitCode,
    bool IsActive,
    long Revision
);
