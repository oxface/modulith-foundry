using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class HistoryMapping
{
    internal static void Configure(
        ModelBuilder model,
        Expression<Func<string>> requiredOrganization
    )
    {
        var stream = model.Entity<EventStream>();
        stream.ToTable(
            "event_streams",
            table => table.HasCheckConstraint("positive_stream_version", "version >= 1")
        );
        stream.HasKey(row => new { row.OrganizationKey, row.Id });
        stream
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);
        stream.Property(row => row.Id).HasColumnName("id").ValueGeneratedNever();
        stream.Property(row => row.StreamType).HasColumnName("stream_type").HasMaxLength(100);
        stream
            .Property(row => row.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        stream.Property(row => row.CreatedAt).HasColumnName("created_at");
        stream.Property(row => row.UpdatedAt).HasColumnName("updated_at");
        stream.HasTenantOwnership(
            row => row.OrganizationKey,
            requiredOrganization,
            "OrganizationScope"
        );

        var stored = model.Entity<StoredEvent>();
        stored.ToTable(
            "events",
            table =>
            {
                table.HasCheckConstraint("positive_event_version", "stream_version >= 1");
                table.HasCheckConstraint("positive_event_schema", "schema_version >= 1");
            }
        );
        stored.HasKey(row => new { row.OrganizationKey, row.EventId });
        stored
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);
        stored.Property(row => row.EventId).HasColumnName("event_id").ValueGeneratedNever();
        stored.Property(row => row.StreamId).HasColumnName("stream_id");
        stored.Property(row => row.StreamVersion).HasColumnName("stream_version");
        stored.Property(row => row.EventName).HasColumnName("event_name").HasMaxLength(200);
        stored.Property(row => row.SchemaVersion).HasColumnName("schema_version");
        stored.Property(row => row.RecordedAt).HasColumnName("recorded_at");
        stored.Property(row => row.Payload).HasColumnName("payload").HasColumnType("jsonb");
        stored
            .HasIndex(row => new
            {
                row.OrganizationKey,
                row.StreamId,
                row.StreamVersion,
            })
            .IsUnique();
        stored
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(row => new { row.OrganizationKey, row.StreamId })
            .OnDelete(DeleteBehavior.Restrict);
        stored.HasTenantOwnership(
            row => row.OrganizationKey,
            requiredOrganization,
            "OrganizationScope"
        );
    }
}
