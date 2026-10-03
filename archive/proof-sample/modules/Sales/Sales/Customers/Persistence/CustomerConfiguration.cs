using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Sales.Customers.Persistence;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    internal const string OrganizationCodeConstraint = "ux_customers_organization_code";

    public void Configure(EntityTypeBuilder<Customer> customer)
    {
        customer.ToTable("customers");
        customer.HasKey(entity => entity.Id).HasName("pk_customers");
        customer.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        customer.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        customer
            .Property(entity => entity.Code)
            .HasColumnName("code")
            .HasMaxLength(CustomerInput.CodeMaximumLength);
        customer
            .Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(CustomerInput.NameMaximumLength);
        customer.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        customer
            .HasIndex(entity => new { entity.OrganizationId, entity.Code })
            .IsUnique()
            .HasDatabaseName(OrganizationCodeConstraint);
    }
}
