using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderWriteModelConfiguration
    : IEntityTypeConfiguration<PurchaseOrderWriteModel>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderWriteModel> entity)
    {
        entity.ToTable("purchase_order_write_models");
        entity.HasKey(x => x.StreamId);
        entity.Property(x => x.StreamId).HasColumnName("stream_id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.State).HasColumnName("state").HasColumnType("jsonb");
        entity
            .HasIndex(x => new { x.OrganizationId, x.Code })
            .IsUnique()
            .HasDatabaseName(PurchaseOrderStore.CodeConstraint);
        entity
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(x => x.StreamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
