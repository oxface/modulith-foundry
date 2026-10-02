using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

internal sealed class PurchasingInboxReceiptConfiguration
    : IEntityTypeConfiguration<PurchasingInboxReceipt>
{
    public void Configure(EntityTypeBuilder<PurchasingInboxReceipt> entity)
    {
        entity.ToTable("inbox_receipts");
        entity.HasKey(x => x.MessageId).HasName("pk_inbox_receipts");
        entity.Property(x => x.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        entity.Property(x => x.Rejected).HasColumnName("rejected");
        entity.Property(x => x.ProcessedAt).HasColumnName("processed_at");
    }
}
