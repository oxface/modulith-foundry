using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class StoredEventConfiguration : IEntityTypeConfiguration<StoredEvent>
{
    public void Configure(EntityTypeBuilder<StoredEvent> storedEvent)
    {
        storedEvent.ToTable("events");
        storedEvent.HasKey(entity => entity.EventId).HasName("pk_events");
        storedEvent
            .Property(entity => entity.GlobalSequence)
            .HasColumnName("global_sequence")
            .ValueGeneratedOnAdd();
        storedEvent
            .Property(entity => entity.EventId)
            .HasColumnName("event_id")
            .ValueGeneratedNever();
        storedEvent.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        storedEvent.Property(entity => entity.StreamId).HasColumnName("stream_id");
        storedEvent.Property(entity => entity.StreamVersion).HasColumnName("stream_version");
        storedEvent
            .Property(entity => entity.EventName)
            .HasColumnName("event_name")
            .HasMaxLength(200);
        storedEvent.Property(entity => entity.SchemaVersion).HasColumnName("schema_version");
        storedEvent.Property(entity => entity.RecordedAt).HasColumnName("recorded_at");
        storedEvent
            .Property(entity => entity.Payload)
            .HasColumnName("payload")
            .HasColumnType("jsonb");
        storedEvent
            .Property(entity => entity.Metadata)
            .HasColumnName("metadata")
            .HasColumnType("jsonb");
        storedEvent
            .HasIndex(entity => entity.GlobalSequence)
            .IsUnique()
            .HasDatabaseName("ux_events_global_sequence");
        storedEvent
            .HasIndex(entity => new { entity.StreamId, entity.StreamVersion })
            .IsUnique()
            .HasDatabaseName("ux_events_stream_version");
        storedEvent
            .HasIndex(entity => new { entity.OrganizationId, entity.RecordedAt })
            .HasDatabaseName("ix_events_organization_recorded_at");
        storedEvent
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(entity => entity.StreamId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_events_event_streams_stream_id");
    }
}
