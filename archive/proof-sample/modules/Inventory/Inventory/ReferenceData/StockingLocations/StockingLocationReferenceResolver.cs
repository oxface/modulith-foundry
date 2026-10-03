using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Contracts;
using ModulithFoundry.Modules.Inventory.Persistence;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;

internal sealed class StockingLocationReferenceResolver(
    InventoryDbContext context,
    IOrganizationContextAccessor accessor
) : IStockingLocationReferenceResolver
{
    public async Task<StockingLocationId?> FindActiveAsync(
        OrganizationId organizationId,
        string code,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId.Value, Guid.Empty);
        if (accessor.OrganizationContext is { } human)
        {
            if (human.OrganizationId != organizationId)
                throw new InvalidOperationException(
                    "The requested organization does not match the resolved context."
                );
        }
        else
            context.UseWorkflowOrganization(organizationId.Value);
        string normalized = InventoryCode.Normalize(code, "code", 64);
        Guid? id = await context
            .StockingLocations.AsNoTracking()
            .Where(location => location.Code == normalized && location.IsActive)
            .Select(location => (Guid?)location.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return id is { } value ? new StockingLocationId(value) : null;
    }
}
