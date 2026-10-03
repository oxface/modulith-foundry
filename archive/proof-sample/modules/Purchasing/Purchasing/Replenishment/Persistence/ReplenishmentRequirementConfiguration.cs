using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Purchasing.Replenishment;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Persistence;

internal sealed class ReplenishmentRequirementConfiguration
    : IEntityTypeConfiguration<ReplenishmentRequirement>
{
    public void Configure(EntityTypeBuilder<ReplenishmentRequirement> entity)
    {
        entity.ToTable("requirements");
        entity.HasKey(x => x.Id).HasName("pk_requirements");
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Number).HasColumnName("number").ValueGeneratedOnAdd();
        entity.Property(x => x.OperationId).HasColumnName("operation_id");
        entity.Property(x => x.StockItemId).HasColumnName("stock_item_id");
        entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(19, 6);
        entity.Property(x => x.Sku).HasColumnName("sku").HasMaxLength(64);
        entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(200);
        entity.Property(x => x.BaseUnitCode).HasColumnName("base_unit_code").HasMaxLength(16);
        entity.Property(x => x.ReferenceRevision).HasColumnName("reference_revision");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.HasIndex(x => x.OperationId).IsUnique().HasDatabaseName("ux_requirements_operation");
        entity
            .HasIndex(x => new { x.OrganizationId, x.Number })
            .IsUnique()
            .HasDatabaseName("ux_requirements_organization_number");
    }
}
