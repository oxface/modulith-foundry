using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemReferenceProjection : IOrganizationOwned
{
    private StockItemReferenceProjection()
    {
        Sku = null!;
        Description = null!;
        BaseUnitCode = null!;
    }

    public Guid OrganizationId { get; private set; }
    internal Guid StockItemId { get; private set; }
    internal string Sku { get; private set; }
    internal string Description { get; private set; }
    internal string BaseUnitCode { get; private set; }
    internal bool IsActive { get; private set; }
    internal long SourceRevision { get; private set; }

    internal static StockItemReferenceProjection From(StockItemReferenceStateV1 state) =>
        new()
        {
            OrganizationId = state.OrganizationId,
            StockItemId = state.StockItemId,
            Sku = state.Sku,
            Description = state.Description,
            BaseUnitCode = state.BaseUnitCode,
            IsActive = state.IsActive,
            SourceRevision = state.Revision,
        };

    internal void Apply(StockItemReferenceStateV1 state)
    {
        if (state.Revision < SourceRevision)
            return;
        if (state.Revision == SourceRevision)
        {
            if (
                state.Sku != Sku
                || state.Description != Description
                || state.BaseUnitCode != BaseUnitCode
                || state.IsActive != IsActive
            )
                throw new InvalidDataException(
                    "Stock Item reference revision was reused with different state."
                );
            return;
        }
        if (state.Sku != Sku || state.BaseUnitCode != BaseUnitCode)
            throw new InvalidDataException("Stock Item immutable reference identity changed.");
        Sku = state.Sku;
        Description = state.Description;
        BaseUnitCode = state.BaseUnitCode;
        IsActive = state.IsActive;
        SourceRevision = state.Revision;
    }
}
