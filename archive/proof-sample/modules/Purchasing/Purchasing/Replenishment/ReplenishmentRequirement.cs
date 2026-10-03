using ModulithFoundry.Modules.Access.Contracts;

namespace ModulithFoundry.Modules.Purchasing.Replenishment;

internal sealed class ReplenishmentRequirement : IOrganizationOwned
{
    private ReplenishmentRequirement()
    {
        Sku = null!;
        Description = null!;
        BaseUnitCode = null!;
    }

    internal Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    internal long Number { get; private set; }
    internal Guid OperationId { get; private set; }
    internal Guid StockItemId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal string Sku { get; private set; }
    internal string Description { get; private set; }
    internal string BaseUnitCode { get; private set; }
    internal long ReferenceRevision { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }

    internal static ReplenishmentRequirement Create(
        Guid organizationId,
        Guid operationId,
        ReplenishmentQuantity quantity,
        ReplenishmentStockItemSnapshot item,
        DateTimeOffset now
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(operationId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfEqual(item.StockItemId, Guid.Empty);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity.Value);
        return new()
        {
            Id = Guid.CreateVersion7(now),
            OrganizationId = organizationId,
            OperationId = operationId,
            StockItemId = item.StockItemId,
            Quantity = quantity.Value,
            Sku = item.Sku,
            Description = item.Description,
            BaseUnitCode = item.BaseUnitCode,
            ReferenceRevision = item.SourceRevision,
            CreatedAt = now,
        };
    }
}
