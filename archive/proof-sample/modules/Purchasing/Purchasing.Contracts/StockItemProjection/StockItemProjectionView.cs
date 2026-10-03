namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record StockItemProjectionView(
    Guid OrganizationId,
    Guid StockItemId,
    string Sku,
    string Description,
    string BaseUnitCode,
    bool IsActive,
    long SourceRevision
);
