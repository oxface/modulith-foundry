using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.Inventory;

public sealed class InventoryDbContext(
    DbContextOptions<InventoryDbContext> options,
    ITenantContextAccessor tenancy
) : DbContext(options)
{
    internal DbSet<StockRow> Stock => Set<StockRow>();
    internal string RequiredOrganizationKey => tenancy.Current.RequireTenant().Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        var stock = modelBuilder.Entity<StockRow>();
        stock.ToTable("stock_availability");
        stock.HasKey(row => row.Id);
        stock.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        stock.Property(row => row.Sku).HasColumnName("sku").HasMaxLength(128);
        stock.Property(row => row.AvailableQuantity).HasColumnName("available_quantity");
        stock.HasIndex(row => new { row.OrganizationKey, row.Sku }).IsUnique();
        stock.HasTenantOwnership(
            row => row.OrganizationKey,
            () => RequiredOrganizationKey,
            "OrganizationScope"
        );
        stock
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.ValidateTenantChanges(() => RequiredOrganizationKey);
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        this.ValidateTenantChanges(() => RequiredOrganizationKey);
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}
