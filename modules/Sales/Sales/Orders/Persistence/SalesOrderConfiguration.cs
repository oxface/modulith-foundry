using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Customers;

namespace ModulithFoundry.Modules.Sales.Orders.Persistence;

internal sealed class SalesOrderConfiguration : IEntityTypeConfiguration<SalesOrder>
{
    public void Configure(EntityTypeBuilder<SalesOrder> order)
    {
        order.ToTable("orders");
        order.HasKey(entity => entity.Id).HasName("pk_orders");
        order.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        order.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        order.Property(entity => entity.CustomerId).HasColumnName("customer_id");
        order.Property(entity => entity.OrderNumber).HasColumnName("order_number");
        order.Property(entity => entity.Currency).HasColumnName("currency").HasMaxLength(3);
        order
            .Property(entity => entity.TotalAmount)
            .HasColumnName("total_amount")
            .HasPrecision(19, 2);
        order.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        order
            .Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion(
                value => SalesOrderStatusValues.ToValue(value),
                value => SalesOrderStatusValues.FromValue(value)
            );
        order.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        order.Property(entity => entity.SubmittedBy).HasColumnName("submitted_by");
        order.Property(entity => entity.SubmittedAt).HasColumnName("submitted_at");
        order.Property(entity => entity.ApprovedBy).HasColumnName("approved_by");
        order.Property(entity => entity.ApprovedAt).HasColumnName("approved_at");
        order.Property(entity => entity.CancelledBy).HasColumnName("cancelled_by");
        order.Property(entity => entity.CancelledAt).HasColumnName("cancelled_at");
        order
            .Property(entity => entity.CancellationReason)
            .HasColumnName("cancellation_reason")
            .HasMaxLength(500);
        order
            .HasIndex(entity => new { entity.OrganizationId, entity.OrderNumber })
            .IsUnique()
            .HasDatabaseName("ux_orders_organization_number");
        order
            .HasOne<Customer>()
            .WithMany()
            .HasForeignKey(entity => entity.CustomerId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_orders_customers");
        order.OwnsMany(
            entity => entity.Lines,
            line =>
            {
                line.ToTable("order_lines");
                line.WithOwner().HasForeignKey("order_id");
                line.Property<Guid>("order_id");
                line.HasKey("order_id", nameof(SalesOrderLine.LineNumber))
                    .HasName("pk_order_lines");
                line.Property(entity => entity.LineNumber)
                    .HasColumnName("line_number")
                    .ValueGeneratedNever();
                line.Property(entity => entity.StockItemId).HasColumnName("stock_item_id");
                line.Property(entity => entity.Sku).HasColumnName("sku").HasMaxLength(64);
                line.Property(entity => entity.Description)
                    .HasColumnName("description")
                    .HasMaxLength(200);
                line.Property(entity => entity.BaseUnitCode)
                    .HasColumnName("base_unit_code")
                    .HasMaxLength(16);
                line.Property(entity => entity.Quantity)
                    .HasColumnName("quantity")
                    .HasPrecision(19, 6);
                line.Property(entity => entity.UnitPrice)
                    .HasColumnName("unit_price")
                    .HasPrecision(19, 4);
                line.Property(entity => entity.LineAmount)
                    .HasColumnName("line_amount")
                    .HasPrecision(19, 2);
            }
        );
        order.Navigation(entity => entity.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
