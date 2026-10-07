using Microsoft.EntityFrameworkCore;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.Purchasing;

public sealed class PurchasingDbContext(
    DbContextOptions<PurchasingDbContext> options,
    ITenantContextAccessor tenancy
) : DbContext(options)
{
    internal DbSet<EventStream> EventStreams => Set<EventStream>();
    internal DbSet<StoredEvent> Events => Set<StoredEvent>();
    internal DbSet<PurchaseOrderCurrentRow> PurchaseOrders => Set<PurchaseOrderCurrentRow>();
    internal DbSet<PurchaseOrderSummaryRow> PurchaseOrderSummaries =>
        Set<PurchaseOrderSummaryRow>();
    internal string RequiredOrganizationKey => tenancy.Current.RequireTenant().Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("purchasing");
        HistoryMapping.Configure(modelBuilder, () => RequiredOrganizationKey);
        InlineViewMapping.Configure(modelBuilder, () => RequiredOrganizationKey);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.ValidateTenantChanges(() => RequiredOrganizationKey);
        this.ValidateEventStreamChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        this.ValidateTenantChanges(() => RequiredOrganizationKey);
        this.ValidateEventStreamChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
