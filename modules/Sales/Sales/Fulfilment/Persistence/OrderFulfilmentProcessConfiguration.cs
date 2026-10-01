using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders;

namespace ModulithFoundry.Modules.Sales.Fulfilment.Persistence;

internal sealed class OrderFulfilmentProcessConfiguration
    : IEntityTypeConfiguration<OrderFulfilmentProcess>
{
    internal const string OrderConstraint = "ux_fulfilment_processes_organization_order";

    public void Configure(EntityTypeBuilder<OrderFulfilmentProcess> process)
    {
        process.ToTable("fulfilment_processes");
        process.HasKey(entity => entity.Id).HasName("pk_fulfilment_processes");
        process.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        process.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        process.Property(entity => entity.OrderId).HasColumnName("order_id");
        process
            .Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion(
                value => OrderFulfilmentStatusValues.ToValue(value),
                value => OrderFulfilmentStatusValues.FromValue(value)
            );
        process.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        process
            .HasIndex(entity => new { entity.OrganizationId, entity.OrderId })
            .IsUnique()
            .HasDatabaseName(OrderConstraint);
        process
            .HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(entity => entity.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_fulfilment_processes_orders");
    }
}
