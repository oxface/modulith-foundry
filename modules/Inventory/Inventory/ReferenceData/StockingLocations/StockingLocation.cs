using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;

internal sealed class StockingLocation : IOrganizationOwned
{
    private StockingLocation()
    {
        Code = null!;
        Name = null!;
    }

    private StockingLocation(
        Guid id,
        Guid organizationId,
        string code,
        string name,
        DateTimeOffset createdAt)
    {
        Id = id;
        OrganizationId = organizationId;
        Code = code;
        Name = name;
        IsActive = true;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    internal Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    internal string Code { get; private set; }

    internal string Name { get; private set; }

    internal bool IsActive { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal static StockingLocation Create(
        Guid id,
        Guid organizationId,
        string code,
        string name,
        DateTimeOffset createdAt) =>
        new(
            id,
            organizationId,
            InventoryCode.Normalize(code, "code", 64),
            InventoryCode.NormalizeText(name, "name", 200),
            createdAt);

    internal bool Rename(string name, DateTimeOffset changedAt)
    {
        string normalized = InventoryCode.NormalizeText(name, "name", 200);
        if (string.Equals(Name, normalized, StringComparison.Ordinal))
        {
            return false;
        }

        Name = normalized;
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
