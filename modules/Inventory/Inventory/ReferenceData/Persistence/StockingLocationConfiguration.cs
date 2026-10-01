using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockingLocations;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.Persistence;

internal sealed class StockingLocationConfiguration : IEntityTypeConfiguration<StockingLocation>
{
    public void Configure(EntityTypeBuilder<StockingLocation> location)
    {
        location.ToTable("stocking_locations");
        location.HasKey(entity => entity.Id).HasName("pk_stocking_locations");
        location.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        location.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        location.Property(entity => entity.Code).HasColumnName("code").HasMaxLength(64);
        location.Property(entity => entity.Name).HasColumnName("name").HasMaxLength(200);
        location.Property(entity => entity.IsActive).HasColumnName("is_active");
        location.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        location.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        location
            .HasIndex(entity => new { entity.OrganizationId, entity.Code })
            .IsUnique()
            .HasDatabaseName("ux_stocking_locations_organization_code");
        location
            .HasIndex(entity => new { entity.OrganizationId, entity.IsActive })
            .HasDatabaseName("ix_stocking_locations_organization_active");
    }
}
