using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

internal sealed class StockItem : IOrganizationOwned
{
    private StockItem()
    {
        Sku = null!;
        Description = null!;
        BaseUnitCode = null!;
    }

    private StockItem(
        Guid id,
        Guid organizationId,
        string sku,
        string description,
        string baseUnitCode,
        DateTimeOffset createdAt
    )
    {
        Id = id;
        OrganizationId = organizationId;
        Sku = sku;
        Description = description;
        BaseUnitCode = baseUnitCode;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal string Sku { get; private set; }

    internal string Description { get; private set; }

    internal string BaseUnitCode { get; private set; }

    internal bool IsActive { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal static StockItem Create(
        Guid id,
        Guid organizationId,
        string sku,
        string description,
        string baseUnitCode,
        DateTimeOffset createdAt
    ) =>
        new(
            id,
            organizationId,
            InventoryCode.Normalize(sku, "sku", 64),
            InventoryCode.NormalizeText(description, "description", 200),
            InventoryCode.Normalize(baseUnitCode, "baseUnitCode", 16),
            createdAt
        );

    internal bool ChangeDescription(string description, DateTimeOffset changedAt)
    {
        string normalized = InventoryCode.NormalizeText(description, "description", 200);
        if (string.Equals(Description, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        Description = normalized;
        UpdatedAt = changedAt;
        return true;
    }

    internal bool SetActive(bool isActive, DateTimeOffset changedAt)
    {
        if (IsActive == isActive)
        {
            return false;
        }

        IsActive = isActive;
        UpdatedAt = changedAt;
        return true;
    }
}
