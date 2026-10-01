using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Inventory.Messaging.Persistence;
using ModulithFoundry.Modules.Inventory.ReferenceData;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;
using ModulithFoundry.Modules.Inventory.Reservations;
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

    internal DbSet<InventoryInboxReceipt> InboxReceipts => Set<InventoryInboxReceipt>();

    internal DbSet<InventoryOutboxMessage> OutboxMessages => Set<InventoryOutboxMessage>();

    internal DbSet<ReservationOperation> ReservationOperations => Set<ReservationOperation>();

    internal DbSet<EventStream> EventStreams => Set<EventStream>();

    internal DbSet<StoredEvent> Events => Set<StoredEvent>();

    internal DbSet<StockPositionWriteModel> StockPositionWriteModels =>
        Set<StockPositionWriteModel>();

    private Guid? workflowOrganizationId;

    internal void UseWorkflowOrganization(Guid organizationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        if (
            organizationContextAccessor.OrganizationContext is not null
            || workflowOrganizationId.HasValue
        )
            throw new InvalidOperationException(
                "Workflow scope requires a fresh non-human Inventory context."
            );
        workflowOrganizationId = organizationId;
    }

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
        organizationContextAccessor.OrganizationContext?.OrganizationId.Value
        ?? workflowOrganizationId;
}
