using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Sales.Messaging.Persistence;

internal sealed class SalesOutboxMessageConfiguration : IEntityTypeConfiguration<SalesOutboxMessage>
{
    public void Configure(EntityTypeBuilder<SalesOutboxMessage> message)
    {
        message.ToTable("outbox_messages");
        message.HasKey(item => item.MessageId).HasName("pk_outbox_messages");
        message.Property(item => item.MessageId).HasColumnName("message_id").ValueGeneratedNever();
        message.Property(item => item.OrganizationId).HasColumnName("organization_id");
        message.Property(item => item.MessageType).HasColumnName("message_type").HasMaxLength(100);
        message.Property(item => item.Payload).HasColumnName("payload").HasColumnType("jsonb");
        message.Property(item => item.CreatedAt).HasColumnName("created_at");
        message.Property(item => item.AvailableAt).HasColumnName("available_at");
        message.Property(item => item.DispatchedAt).HasColumnName("dispatched_at");
        message.Property(item => item.LeaseToken).HasColumnName("lease_token");
        message.Property(item => item.LeaseUntil).HasColumnName("lease_until");
        message.Property(item => item.Attempts).HasColumnName("attempts");
        message
            .HasIndex(item => item.AvailableAt)
            .HasFilter("dispatched_at IS NULL")
            .HasDatabaseName("ix_outbox_messages_pending");
    }
}
