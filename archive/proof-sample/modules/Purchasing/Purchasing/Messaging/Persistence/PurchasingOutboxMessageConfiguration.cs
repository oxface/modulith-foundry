using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

namespace ModulithFoundry.Modules.Purchasing.Messaging.Persistence;

internal sealed class PurchasingOutboxMessageConfiguration
    : IEntityTypeConfiguration<PurchasingOutboxMessage>
{
    public void Configure(EntityTypeBuilder<PurchasingOutboxMessage> entity)
    {
        entity.ToTable("outbox_messages");
        entity.HasKey(x => x.MessageId).HasName("pk_outbox_messages");
        entity.Property(x => x.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.MessageType).HasColumnName("message_type").HasMaxLength(100);
        entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.AvailableAt).HasColumnName("available_at");
        entity.Property(x => x.DispatchedAt).HasColumnName("dispatched_at");
        entity.Property(x => x.LeaseToken).HasColumnName("lease_token");
        entity.Property(x => x.LeaseUntil).HasColumnName("lease_until");
        entity.Property(x => x.Attempts).HasColumnName("attempts");
        entity
            .HasIndex(x => x.AvailableAt)
            .HasFilter("dispatched_at IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
