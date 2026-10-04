using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.PersistenceDemo.Sales;

public sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    ITenantContextAccessor tenancy
) : DbContext(options)
{
    public DbSet<CustomerReference> Customers => Set<CustomerReference>();
    private string RequiredOrganizationKey => tenancy.Current.RequireTenant().Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sales");
        var customer = modelBuilder.Entity<CustomerReference>();
        customer.ToTable("customer_reference");
        customer.HasKey(row => row.Id);
        customer.Property(row => row.Id).ValueGeneratedNever();
        customer.Property(row => row.Code).IsRequired();
        customer.Property(row => row.DisplayName).IsRequired();
        customer.HasIndex(row => new { row.OrganizationKey, row.Code }).IsUnique();
        customer.HasTenantOwnership(
            row => row.OrganizationKey,
            () => RequiredOrganizationKey,
            "OrganizationScope"
        );
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
