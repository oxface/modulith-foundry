using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

namespace ModulithFoundry.Modules.Inventory.Persistence;

internal sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options,
    IOrganizationContextAccessor organizationContextAccessor) : DbContext(options)
{
    internal const string Schema = "inventory";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<StockItem> StockItems => Set<StockItem>();

    internal DbSet<StockingLocation> StockingLocations => Set<StockingLocation>();

    internal DbSet<InventoryAuditEntry> AuditEntries => Set<InventoryAuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
        modelBuilder.Entity<StockItem>().HasQueryFilter(
            OrganizationScopeFilter,
            item => CurrentOrganizationId.HasValue
                && item.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<StockingLocation>().HasQueryFilter(
            OrganizationScopeFilter,
            location => CurrentOrganizationId.HasValue
                && location.OrganizationId == CurrentOrganizationId);
        modelBuilder.Entity<InventoryAuditEntry>().HasQueryFilter(
            OrganizationScopeFilter,
            audit => CurrentOrganizationId.HasValue
                && audit.OrganizationId == CurrentOrganizationId);
    }

    private Guid? CurrentOrganizationId =>
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value;
}
