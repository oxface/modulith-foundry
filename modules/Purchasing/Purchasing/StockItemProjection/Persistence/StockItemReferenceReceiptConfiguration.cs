using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.StockItemProjection.Persistence;

internal sealed class StockItemReferenceReceiptConfiguration
    : IEntityTypeConfiguration<StockItemReferenceReceipt>
{
    public void Configure(EntityTypeBuilder<StockItemReferenceReceipt> receipt)
    {
        receipt.ToTable("stock_item_reference_inbox");
        receipt.HasKey(x => x.MessageId);
        receipt.Property(x => x.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        receipt.Property(x => x.OrganizationId).HasColumnName("organization_id");
        receipt.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        receipt.Property(x => x.ProcessedAt).HasColumnName("processed_at");
    }
}
