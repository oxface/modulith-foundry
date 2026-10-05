using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;
using ModulithFoundry.Tenancy;

namespace ModulithFoundry.Samples.Wholesale.Sales;

public sealed class SalesDbContext(
    DbContextOptions<SalesDbContext> options,
    ITenantContextAccessor tenancy
) : DbContext(options)
{
    internal DbSet<CustomerRow> Customers => Set<CustomerRow>();
    internal DbSet<AddressRow> Addresses => Set<AddressRow>();
    internal string RequiredOrganizationKey => tenancy.Current.RequireTenant().Value;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("sales");
        var customer = modelBuilder.Entity<CustomerRow>();
        customer.ToTable(
            "customer_profiles",
            table => table.HasCheckConstraint("positive_version", "version >= 1")
        );
        customer.HasKey(row => row.Id);
        customer.HasAlternateKey(row => new { row.OrganizationKey, row.Id });
        customer.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        customer.Property(row => row.Code).HasColumnName("code").HasMaxLength(128);
        customer.Property(row => row.DisplayName).HasColumnName("display_name").HasMaxLength(256);
        customer
            .Property(row => row.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        customer.HasIndex(row => new { row.OrganizationKey, row.Code }).IsUnique();
        customer.HasTenantOwnership(
            row => row.OrganizationKey,
            () => RequiredOrganizationKey,
            "OrganizationScope"
        );
        customer
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);

        var address = modelBuilder.Entity<AddressRow>();
        address.ToTable("customer_addresses");
        address.HasKey(row => row.Id);
        address.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        address.Property(row => row.CustomerId).HasColumnName("customer_id");
        address.Property(row => row.AddressLine).HasColumnName("address_line").HasMaxLength(512);
        // This profile demonstration has exactly one address per customer.
        address.HasIndex(row => new { row.OrganizationKey, row.CustomerId }).IsUnique();
        address
            .HasOne<CustomerRow>()
            .WithMany()
            .HasForeignKey(row => new { row.OrganizationKey, row.CustomerId })
            .HasPrincipalKey(row => new { row.OrganizationKey, row.Id })
            .OnDelete(DeleteBehavior.Restrict);
        address.HasTenantOwnership(
            row => row.OrganizationKey,
            () => RequiredOrganizationKey,
            "OrganizationScope"
        );
        address
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
