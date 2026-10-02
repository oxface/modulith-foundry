using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Purchasing.PurchaseOrders.Persistence;

internal sealed class PurchaseOrderSummaryConfiguration
    : IEntityTypeConfiguration<PurchaseOrderSummary>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderSummary> entity)
    {
        entity.ToTable("purchase_order_summaries");
        entity.HasKey(x => x.StreamId);
        entity.Property(x => x.StreamId).HasColumnName("stream_id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(32);
        entity.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3);
        entity.Property(x => x.Version).HasColumnName("version");
        entity.Property(x => x.IsIssued).HasColumnName("is_issued");
        entity.Property(x => x.LineCount).HasColumnName("line_count");
        entity.Property(x => x.Total).HasColumnName("total").HasPrecision(20, 5);
        entity.Property(x => x.LineAmounts).HasColumnName("line_amounts").HasColumnType("jsonb");
        entity
            .HasIndex(x => new { x.OrganizationId, x.Code })
            .HasDatabaseName("ix_purchase_order_summaries_organization_code");
        entity
            .HasOne<EventStream>()
            .WithMany()
            .HasForeignKey(x => x.StreamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
