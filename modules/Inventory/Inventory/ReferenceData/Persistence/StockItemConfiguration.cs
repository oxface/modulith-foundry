using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.Persistence;

internal sealed class StockItemConfiguration : IEntityTypeConfiguration<StockItem>
{
    public void Configure(EntityTypeBuilder<StockItem> item)
    {
        item.ToTable("stock_items");
        item.HasKey(entity => entity.Id).HasName("pk_stock_items");
        item.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        item.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        item.Property(entity => entity.Sku).HasColumnName("sku").HasMaxLength(64);
        item.Property(entity => entity.Description).HasColumnName("description").HasMaxLength(200);
        item.Property(entity => entity.BaseUnitCode).HasColumnName("base_unit_code").HasMaxLength(16);
        item.Property(entity => entity.IsActive).HasColumnName("is_active");
        item.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        item.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        item.HasIndex(entity => new { entity.OrganizationId, entity.Sku })
            .IsUnique()
            .HasDatabaseName("ux_stock_items_organization_sku");
        item.HasIndex(entity => new { entity.OrganizationId, entity.IsActive })
            .HasDatabaseName("ix_stock_items_organization_active");
    }
}
