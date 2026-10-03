using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Sales.Orders.Persistence;

internal sealed class SalesOrderNumberConfiguration : IEntityTypeConfiguration<SalesOrderNumber>
{
    public void Configure(EntityTypeBuilder<SalesOrderNumber> number)
    {
        number.ToTable("order_numbers");
        number.HasKey(entity => entity.OrganizationId).HasName("pk_order_numbers");
        number
            .Property(entity => entity.OrganizationId)
            .HasColumnName("organization_id")
            .ValueGeneratedNever();
        number.Property(entity => entity.LastNumber).HasColumnName("last_number");
    }
}
