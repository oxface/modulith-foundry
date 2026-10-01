using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders.Activity;

namespace ModulithFoundry.Modules.Sales.Orders.Persistence;

internal sealed class SalesOrderActivityConfiguration : IEntityTypeConfiguration<SalesOrderActivity>
{
    public void Configure(EntityTypeBuilder<SalesOrderActivity> activity)
    {
        activity.ToTable("order_activity");
        activity.HasKey(entry => entry.Id).HasName("pk_order_activity");
        activity.Property(entry => entry.Id).HasColumnName("id").ValueGeneratedNever();
        activity.Property(entry => entry.OrganizationId).HasColumnName("organization_id");
        activity.Property(entry => entry.OrderId).HasColumnName("order_id");
        activity.Property(entry => entry.ActorUserId).HasColumnName("actor_user_id");
        activity.Property(entry => entry.OrderVersion).HasColumnName("order_version");
        activity.Property(entry => entry.OccurredAt).HasColumnName("occurred_at");
        activity
            .Property(entry => entry.Kind)
            .HasColumnName("kind")
            .HasMaxLength(32)
            .HasConversion(
                value => SalesOrderActivityKindValues.ToValue(value),
                value => SalesOrderActivityKindValues.FromValue(value)
            );
        activity
            .HasIndex(entry => new
            {
                entry.OrganizationId,
                entry.OrderId,
                entry.OrderVersion,
            })
            .HasDatabaseName("ix_order_activity_organization_order_version");
        activity
            .HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(entry => entry.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_order_activity_orders");
    }
}
