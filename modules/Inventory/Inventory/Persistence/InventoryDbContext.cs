using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;
using ModulithFoundry.Modules.Inventory.StockPositions.Persistence;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Inventory.Persistence;

internal sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor
) : DbContext(options)
{
    internal const string Schema = "inventory";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<StockItem> StockItems => Set<StockItem>();

    internal DbSet<StockingLocation> StockingLocations => Set<StockingLocation>();

    internal DbSet<InventoryAuditEntry> AuditEntries => Set<InventoryAuditEntry>();

    internal DbSet<EventStream> EventStreams => Set<EventStream>();

    internal DbSet<StoredEvent> Events => Set<StoredEvent>();

    internal DbSet<StockPositionWriteModel> StockPositionWriteModels =>
        Set<StockPositionWriteModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(
            OrganizationScopeFilter,
            entity =>
                CurrentOrganizationId.HasValue && entity.OrganizationId == CurrentOrganizationId
        );
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value;
}
