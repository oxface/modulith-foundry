using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Purchasing.Replenishment.Requests;

namespace ModulithFoundry.Modules.Purchasing.Replenishment.Requests.Persistence;

internal sealed class ReplenishmentRequestConfiguration
    : IEntityTypeConfiguration<ReplenishmentRequest>
{
    public void Configure(EntityTypeBuilder<ReplenishmentRequest> entity)
    {
        entity.ToTable("replenishment_requests");
        entity.HasKey(x => x.OperationId).HasName("pk_replenishment_requests");
        entity.Property(x => x.OperationId).HasColumnName("operation_id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.FirstMessageId).HasColumnName("first_message_id");
        entity.Property(x => x.ProcessId).HasColumnName("process_id");
        entity.Property(x => x.OrderNumber).HasColumnName("order_number");
        entity.Property(x => x.LineNumber).HasColumnName("line_number");
        entity.Property(x => x.StockItemId).HasColumnName("stock_item_id");
        entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(19, 6);
        entity.Property(x => x.BaseUnitCode).HasColumnName("base_unit_code").HasMaxLength(16);
        entity
            .Property(x => x.MinimumReferenceRevision)
            .HasColumnName("minimum_reference_revision");
        entity.Property(x => x.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        entity.Property(x => x.ReceivedAt).HasColumnName("received_at");
        entity.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        entity.Property(x => x.RequirementId).HasColumnName("requirement_id");
        entity.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(100);
        entity.Ignore(x => x.IsPending);
        entity.Ignore(x => x.Status);
        entity
            .HasIndex(x => x.NextAttemptAt)
            .HasFilter("requirement_id IS NULL AND reason_code IS NULL")
            .HasDatabaseName("ix_replenishment_requests_pending");
    }
}
