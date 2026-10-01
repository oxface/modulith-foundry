using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Inventory.Reservations.Persistence;

internal sealed class ReservationOperationConfiguration
    : IEntityTypeConfiguration<ReservationOperation>
{
    public void Configure(EntityTypeBuilder<ReservationOperation> operation)
    {
        operation.ToTable("reservation_operations");
        operation
            .HasKey(item => new { item.OrganizationId, item.OperationId })
            .HasName("pk_reservation_operations");
        operation
            .Property(item => item.OperationId)
            .HasColumnName("operation_id")
            .ValueGeneratedNever();
        operation.Property(item => item.OrganizationId).HasColumnName("organization_id");
        operation.Property(item => item.Fingerprint).HasColumnName("fingerprint").HasMaxLength(64);
        operation.Property(item => item.Outcome).HasColumnName("outcome").HasColumnType("jsonb");
        operation.Property(item => item.CreatedAt).HasColumnName("created_at");
    }
}
