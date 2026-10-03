using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class EventStreamConfiguration : IEntityTypeConfiguration<EventStream>
{
    public void Configure(EntityTypeBuilder<EventStream> entity)
    {
        entity.ToTable("event_streams");
        entity.HasKey(x => x.Id).HasName("pk_event_streams");
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.StreamType).HasColumnName("stream_type").HasMaxLength(100);
        entity.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        entity
            .HasIndex(x => new { x.OrganizationId, x.StreamType })
            .HasDatabaseName("ix_event_streams_organization_type");
    }
}
