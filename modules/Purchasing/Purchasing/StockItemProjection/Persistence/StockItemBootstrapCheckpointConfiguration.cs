using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemBootstrapCheckpointConfiguration
    : IEntityTypeConfiguration<StockItemBootstrapCheckpoint>
{
    public void Configure(EntityTypeBuilder<StockItemBootstrapCheckpoint> checkpoint)
    {
        checkpoint.ToTable(
            "stock_item_bootstrap",
            table => table.HasCheckConstraint("ck_stock_item_bootstrap_singleton", "id = 1")
        );
        checkpoint.HasKey(x => x.Id);
        checkpoint.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        checkpoint.Property(x => x.IsReady).HasColumnName("is_ready");
        checkpoint.Property(x => x.SnapshotWatermark).HasColumnName("snapshot_watermark");
        checkpoint.HasData(new StockItemBootstrapCheckpoint());
    }
}
