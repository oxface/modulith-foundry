using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Inventory.ReferenceData.StockItems.Export;

namespace ModulithFoundry.Modules.Inventory.ReferenceData.Persistence;

internal sealed class StockItemReferenceFeedConfiguration
    : IEntityTypeConfiguration<StockItemReferenceFeed>
{
    public void Configure(EntityTypeBuilder<StockItemReferenceFeed> feed)
    {
        feed.ToTable(
            "stock_item_reference_feed",
            table =>
                table.HasCheckConstraint(
                    "ck_stock_item_reference_feed_singleton",
                    "id = 1 AND revision >= 0"
                )
        );
        feed.HasKey(x => x.Id);
        feed.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        feed.Property(x => x.Revision).HasColumnName("revision");
        feed.HasData(new StockItemReferenceFeed());
    }
}
