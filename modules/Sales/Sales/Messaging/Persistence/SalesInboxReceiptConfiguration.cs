using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Sales.Messaging.Persistence;

internal sealed class SalesInboxReceiptConfiguration : IEntityTypeConfiguration<SalesInboxReceipt>
{
    public void Configure(EntityTypeBuilder<SalesInboxReceipt> receipt)
    {
        receipt.ToTable("inbox_receipts");
        receipt.HasKey(item => item.MessageId).HasName("pk_inbox_receipts");
        receipt.Property(item => item.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        receipt.Property(item => item.OrganizationId).HasColumnName("organization_id");
        receipt.Property(item => item.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        receipt.Property(item => item.ProcessedAt).HasColumnName("processed_at");
    }
}
