using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Inventory.StockPositions.Persistence;

internal sealed class EventStreamConfiguration : IEntityTypeConfiguration<EventStream>
{
    public void Configure(EntityTypeBuilder<EventStream> stream)
    {
        stream.ToTable("event_streams");
        stream.HasKey(entity => entity.Id).HasName("pk_event_streams");
        stream.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        stream.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        stream.Property(entity => entity.StreamType).HasColumnName("stream_type").HasMaxLength(100);
        stream.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        stream.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        stream.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
        stream
            .HasIndex(entity => new { entity.OrganizationId, entity.StreamType })
            .HasDatabaseName("ix_event_streams_organization_type");
    }
}
