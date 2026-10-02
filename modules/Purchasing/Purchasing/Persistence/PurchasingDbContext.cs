using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Modules.Access.Contracts;
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
        organizationContext.OrganizationContext?.OrganizationId.Value;
}
