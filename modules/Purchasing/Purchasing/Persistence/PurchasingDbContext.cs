using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
using ModulithFoundry.Modules.Purchasing.Audit;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;
using ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;
using ModulithFoundry.Modules.Purchasing.Replenishment;
using ModulithFoundry.Modules.Purchasing.Replenishment.Requests;
using ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;
using ModulithFoundry.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Persistence;

internal sealed class PurchasingDbContext(
    DbContextOptions<PurchasingDbContext> options,
    IOrganizationContextAccessor organizationContext
) : DbContext(options)
{
    internal const string Schema = "purchasing";
    internal const string OrganizationScopeFilter = "OrganizationScope";

    internal DbSet<StockItemReferenceProjection> StockItemReferences =>
        Set<StockItemReferenceProjection>();

    internal DbSet<StockItemBootstrapCheckpoint> StockItemBootstrapCheckpoints =>
        Set<StockItemBootstrapCheckpoint>();

    internal DbSet<StockItemReferenceReceipt> StockItemReferenceInbox =>
        Set<StockItemReferenceReceipt>();

    internal DbSet<ReplenishmentRequirement> Requirements => Set<ReplenishmentRequirement>();

    internal DbSet<ReplenishmentRequest> ReplenishmentRequests => Set<ReplenishmentRequest>();

    internal DbSet<PurchasingInboxReceipt> InboxReceipts => Set<PurchasingInboxReceipt>();

    internal DbSet<PurchasingOutboxMessage> OutboxMessages => Set<PurchasingOutboxMessage>();

    internal DbSet<PurchasingAuditEntry> AuditEntries => Set<PurchasingAuditEntry>();

    internal DbSet<EventStream> EventStreams => Set<EventStream>();

    internal DbSet<StoredEvent> Events => Set<StoredEvent>();

    internal DbSet<PurchaseOrderWriteModel> PurchaseOrderWriteModels =>
        Set<PurchaseOrderWriteModel>();

    internal DbSet<PurchaseOrderSummary> PurchaseOrderSummaries => Set<PurchaseOrderSummary>();

    private Guid? workflowOrganizationId;

    internal void UseWorkflowOrganization(Guid organizationId)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(organizationId, Guid.Empty);
        if (workflowOrganizationId is { } existing && existing != organizationId)
            throw new InvalidOperationException(
                "One Purchasing operation cannot switch Organizations."
            );
        workflowOrganizationId = organizationId;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PurchasingDbContext).Assembly);
        modelBuilder.ApplyOwnershipFilters<IOrganizationOwned>(
            OrganizationScopeFilter,
            entity =>
                CurrentOrganizationId.HasValue && entity.OrganizationId == CurrentOrganizationId
        );
    }

    private Guid? CurrentOrganizationId =>
        workflowOrganizationId ?? organizationContext.OrganizationContext?.OrganizationId.Value;
}
