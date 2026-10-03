namespace ModulithFoundry.Modules.Purchasing.Contracts;

public sealed record ReplenishmentRequirementView(
    Guid RequirementId,
    long Number,
    Guid StockItemId,
    string Sku,
    string Description,
    decimal Quantity,
    string BaseUnitCode,
    long ReferenceRevision,
    DateTimeOffset CreatedAt
);
