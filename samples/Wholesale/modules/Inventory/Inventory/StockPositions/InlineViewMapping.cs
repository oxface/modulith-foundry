using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Inventory.StockPositions;

internal static class InlineViewMapping
{
    internal static void Configure(
        ModelBuilder model,
        Expression<Func<string>> requiredOrganization
    )
    {
        var current = model.Entity<StockPositionCurrentRow>();
        current.ToTable(
            "stock_position_current",
            table => table.HasCheckConstraint("ck_stock_position_current_version", "version > 0")
        );
        current.HasKey(row => new { row.OrganizationKey, row.StreamId });
        current
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);
        current.Property(row => row.StreamId).HasColumnName("stream_id").ValueGeneratedNever();
        current
            .Property(row => row.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        current.Property(row => row.RecordedAt).HasColumnName("recorded_at");
        current.Property(row => row.State).HasColumnName("state").HasColumnType("jsonb");
        current
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(row => new { row.OrganizationKey, row.StreamId })
            .OnDelete(DeleteBehavior.Restrict);
        current.HasTenantOwnership(
            row => row.OrganizationKey,
            requiredOrganization,
            "OrganizationScope"
        );
    }
}
