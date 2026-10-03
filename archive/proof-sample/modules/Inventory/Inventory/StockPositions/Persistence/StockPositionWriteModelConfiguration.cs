using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StockPositionWriteModelConfiguration
    : IEntityTypeConfiguration<StockPositionWriteModel>
{
    internal const string IdentityConstraint = "ux_stock_position_current_identity";

    public void Configure(EntityTypeBuilder<StockPositionWriteModel> position)
    {
        position.ToTable(
            "stock_position_current",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_stock_position_current_non_negative",
                    "on_hand_quantity >= 0 AND reserved_quantity >= 0 AND available_quantity >= 0"
                );
                table.HasCheckConstraint(
                    "ck_stock_position_current_quantity_balance",
                    "reserved_quantity <= on_hand_quantity AND available_quantity = on_hand_quantity - reserved_quantity"
                );
            }
        );
        position.HasKey(entity => entity.StreamId).HasName("pk_stock_position_current");
        position
            .Property(entity => entity.StreamId)
            .HasColumnName("stream_id")
            .ValueGeneratedNever();
        position.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        position.Property(entity => entity.StockItemId).HasColumnName("stock_item_id");
        position
            .Property(entity => entity.StockingLocationId)
            .HasColumnName("stocking_location_id");
        position
            .Property(entity => entity.BaseUnitCode)
            .HasColumnName("base_unit_code")
            .HasMaxLength(16);
        position
            .Property(entity => entity.OnHandQuantity)
            .HasColumnName("on_hand_quantity")
            .HasPrecision(19, 6);
        position
            .Property(entity => entity.ReservedQuantity)
            .HasColumnName("reserved_quantity")
            .HasPrecision(19, 6);
        position
            .Property(entity => entity.AvailableQuantity)
            .HasColumnName("available_quantity")
            .HasPrecision(19, 6);
        position.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        position.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        position
            .Property(entity => entity.Reservations)
            .HasColumnName("reservations")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'::jsonb");
        position
            .HasIndex(entity => new
            {
                entity.OrganizationId,
                entity.StockingLocationId,
                entity.StockItemId,
            })
            .IsUnique()
            .HasDatabaseName(IdentityConstraint);
        position
            .HasOne<EventStream>()
            .WithOne()
            .HasForeignKey<StockPositionWriteModel>(entity => entity.StreamId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_stock_position_current_event_streams_stream_id");
    }
}
