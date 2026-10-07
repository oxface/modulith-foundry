using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ModulithFoundry.EventSourcing.EntityFrameworkCore;
using ModulithFoundry.Persistence.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Purchasing.PurchaseOrders;

internal static class InlineViewMapping
{
    internal static void Configure(
        ModelBuilder model,
        Expression<Func<string>> requiredOrganization
    )
    {
        var current = model.Entity<PurchaseOrderCurrentRow>();
        current.ToTable(
            "purchase_order_current",
            table => table.HasCheckConstraint("ck_purchase_order_current_version", "version > 0")
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
        model.ConfigureRequiredInlineState<EventStream, StoredEvent, PurchaseOrderCurrentRow>(
            PurchaseOrderHistoryReader.StreamType
        );
        current.HasTenantOwnership(
            row => row.OrganizationKey,
            requiredOrganization,
            "OrganizationScope"
        );

        var summary = model.Entity<PurchaseOrderSummaryRow>();
        summary.ToTable(
            "purchase_order_summary",
            table =>
            {
                table.HasCheckConstraint("ck_purchase_order_summary_version", "version > 0");
                table.HasCheckConstraint(
                    "ck_purchase_order_summary_amounts",
                    "line_count >= 0 AND total >= 0"
                );
            }
        );
        summary.HasKey(row => new { row.OrganizationKey, row.StreamId });
        summary
            .Property(row => row.OrganizationKey)
            .HasColumnName("organization_key")
            .HasMaxLength(256);
        summary.Property(row => row.StreamId).HasColumnName("stream_id").ValueGeneratedNever();
        summary
            .Property(row => row.Version)
            .HasColumnName("version")
            .IsConcurrencyToken()
            .ValueGeneratedNever();
        summary.Property(row => row.RecordedAt).HasColumnName("recorded_at");
        summary.Property(row => row.Code).HasColumnName("code");
        summary.Property(row => row.Currency).HasColumnName("currency");
        summary.Property(row => row.LineCount).HasColumnName("line_count");
        summary.Property(row => row.Total).HasColumnName("total");
        summary
            .Property(row => row.LineAmounts)
            .HasColumnName("line_amounts")
            .HasColumnType("jsonb");
        summary
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(row => new { row.OrganizationKey, row.StreamId })
            .OnDelete(DeleteBehavior.Restrict);
        summary.HasTenantOwnership(
            row => row.OrganizationKey,
            requiredOrganization,
            "OrganizationScope"
        );
        model.ConfigureRequiredInlineState<EventStream, StoredEvent, PurchaseOrderSummaryRow>(
            PurchaseOrderHistoryReader.StreamType,
            isMainState: false
        );
    }
}
