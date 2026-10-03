using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Sales.Contracts;
using ModulithFoundry.Modules.Sales.Orders;

namespace ModulithFoundry.Modules.Sales.Fulfilment.Persistence;

internal sealed class OrderFulfilmentProcessConfiguration
    : IEntityTypeConfiguration<OrderFulfilmentProcess>
{
    internal const string OrderConstraint = "ux_fulfilment_processes_organization_order";

    public void Configure(EntityTypeBuilder<OrderFulfilmentProcess> process)
    {
        process.ToTable("fulfilment_processes");
        process.HasKey(entity => entity.Id).HasName("pk_fulfilment_processes");
        process.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        process.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        process.Property(entity => entity.OrderId).HasColumnName("order_id");
        process
            .Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion(
                value => OrderFulfilmentStatusValues.ToValue(value),
                value => OrderFulfilmentStatusValues.FromValue(value)
            );
        process.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        process.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        process.Property(entity => entity.OrderNumber).HasColumnName("order_number");
        process.Property(entity => entity.StockingLocationId).HasColumnName("stocking_location_id");
        process
            .Property(entity => entity.CancellationRequested)
            .HasColumnName("cancellation_requested");
        process.OwnsMany(
            entity => entity.Lines,
            line =>
            {
                line.ToTable("fulfilment_lines");
                line.WithOwner().HasForeignKey("process_id");
                line.Property<Guid>("process_id").HasColumnName("process_id");
                line.HasKey("process_id", nameof(OrderFulfilmentLine.LineNumber))
                    .HasName("pk_fulfilment_lines");
                line.Property(entity => entity.LineNumber)
                    .HasColumnName("line_number")
                    .ValueGeneratedNever();
                line.Property(entity => entity.StockItemId).HasColumnName("stock_item_id");
                line.Property(entity => entity.Quantity)
                    .HasColumnName("quantity")
                    .HasPrecision(19, 6);
                line.Property(entity => entity.BaseUnitCode)
                    .HasColumnName("base_unit_code")
                    .HasMaxLength(16);
                line.Property(entity => entity.OperationId).HasColumnName("operation_id");
                line.Property(entity => entity.CommandMessageId)
                    .HasColumnName("command_message_id");
                line.Property(entity => entity.Status)
                    .HasColumnName("status")
                    .HasMaxLength(32)
                    .HasConversion(
                        value => OrderFulfilmentLineStatusValues.ToValue(value),
                        value => OrderFulfilmentLineStatusValues.FromValue(value)
                    );
                line.Property(entity => entity.ReservationId).HasColumnName("reservation_id");
                line.Property(entity => entity.AvailableQuantity)
                    .HasColumnName("available_quantity")
                    .HasPrecision(19, 6);
                line.Property(entity => entity.ReasonCode)
                    .HasColumnName("reason_code")
                    .HasMaxLength(100);
                line.Property(entity => entity.OutcomeFingerprint)
                    .HasColumnName("outcome_fingerprint")
                    .HasMaxLength(64);
                line.Property(entity => entity.AttemptCount).HasColumnName("attempt_count");
                line.Property(entity => entity.ResponseDeadline).HasColumnName("response_deadline");
                line.Property(entity => entity.ReplenishmentCommandMessageId)
                    .HasColumnName("replenishment_command_message_id");
                line.Property(entity => entity.ReplenishmentQuantity)
                    .HasColumnName("replenishment_quantity")
                    .HasPrecision(19, 6);
                line.Property(entity => entity.ReplenishmentRequirementId)
                    .HasColumnName("replenishment_requirement_id");
                line.Property(entity => entity.ReplenishmentRequirementNumber)
                    .HasColumnName("replenishment_requirement_number");
                line.Property(entity => entity.ReplenishmentReasonCode)
                    .HasColumnName("replenishment_reason_code")
                    .HasMaxLength(100);
                line.Property(entity => entity.ReplenishmentOutcomeFingerprint)
                    .HasColumnName("replenishment_outcome_fingerprint")
                    .HasMaxLength(64);
                line.Property(entity => entity.ReleaseOperationId)
                    .HasColumnName("release_operation_id");
                line.Property(entity => entity.ReleaseCommandMessageId)
                    .HasColumnName("release_command_message_id");
                line.Property(entity => entity.ReleaseStatus)
                    .HasColumnName("release_status")
                    .HasMaxLength(32)
                    .HasConversion(
                        value => OrderFulfilmentReleaseStatusValues.ToValue(value!.Value),
                        value =>
                            (OrderFulfilmentReleaseStatus?)
                                OrderFulfilmentReleaseStatusValues.FromValue(value)
                    );
                line.Property(entity => entity.ReleaseReasonCode)
                    .HasColumnName("release_reason_code")
                    .HasMaxLength(100);
                line.Property(entity => entity.ReleaseOutcomeFingerprint)
                    .HasColumnName("release_outcome_fingerprint")
                    .HasMaxLength(64);
                line.Property(entity => entity.ReleaseResponseDeadline)
                    .HasColumnName("release_response_deadline");
                line.HasIndex(entity => entity.OperationId)
                    .IsUnique()
                    .HasDatabaseName("ux_fulfilment_lines_operation");
                line.HasIndex(entity => entity.CommandMessageId)
                    .IsUnique()
                    .HasDatabaseName("ux_fulfilment_lines_command");
            }
        );
        process.Navigation(entity => entity.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);
        process
            .HasIndex(entity => new { entity.OrganizationId, entity.OrderId })
            .IsUnique()
            .HasDatabaseName(OrderConstraint);
        process
            .HasOne<SalesOrder>()
            .WithMany()
            .HasForeignKey(entity => entity.OrderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_fulfilment_processes_orders");
    }
}
