using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class StoredEventConfiguration : IEntityTypeConfiguration<StoredEvent>
{
    public void Configure(EntityTypeBuilder<StoredEvent> entity)
    {
        entity.ToTable("events");
        entity.HasKey(x => x.EventId).HasName("pk_events");
        entity.Property(x => x.EventId).HasColumnName("event_id").ValueGeneratedNever();
        entity
            .Property(x => x.GlobalSequence)
            .HasColumnName("global_sequence")
            .ValueGeneratedOnAdd();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.StreamId).HasColumnName("stream_id");
        entity.Property(x => x.StreamVersion).HasColumnName("stream_version");
        entity.Property(x => x.EventName).HasColumnName("event_name").HasMaxLength(200);
        entity.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        entity.Property(x => x.RecordedAt).HasColumnName("recorded_at");
        entity.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
        entity.Property(x => x.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        entity
            .HasIndex(x => x.GlobalSequence)
            .IsUnique()
            .HasDatabaseName("ux_events_global_sequence");
        entity
            .HasIndex(x => new { x.StreamId, x.StreamVersion })
            .IsUnique()
            .HasDatabaseName("ux_events_stream_version");
        entity
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(x => x.StreamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
