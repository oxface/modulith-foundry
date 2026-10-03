using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemReferenceProjectionConfiguration
    : IEntityTypeConfiguration<StockItemReferenceProjection>
{
    public void Configure(EntityTypeBuilder<StockItemReferenceProjection> item)
    {
        item.ToTable("stock_item_references");
        item.HasKey(x => new { x.OrganizationId, x.StockItemId });
        item.Property(x => x.OrganizationId).HasColumnName("organization_id");
        item.Property(x => x.StockItemId).HasColumnName("stock_item_id").ValueGeneratedNever();
        item.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
        item.Property(x => x.Description).HasColumnName("description").HasMaxLength(200);
        item.Property(x => x.BaseUnitCode).HasColumnName("base_unit_code").HasMaxLength(16);
        item.Property(x => x.IsActive).HasColumnName("is_active");
        item.Property(x => x.SourceRevision).HasColumnName("source_revision");
    }
}
