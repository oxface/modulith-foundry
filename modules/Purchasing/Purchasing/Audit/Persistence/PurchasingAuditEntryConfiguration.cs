using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Purchasing.Audit;

namespace ModulithFoundry.Modules.Purchasing.Audit.Persistence;

internal sealed class PurchasingAuditEntryConfiguration
    : IEntityTypeConfiguration<PurchasingAuditEntry>
{
    public void Configure(EntityTypeBuilder<PurchasingAuditEntry> entity)
    {
        entity.ToTable(
            "audit_entries",
            table =>
                table.HasCheckConstraint(
                    "ck_audit_entries_actor",
                    "(actor_user_id IS NULL) <> (system_actor IS NULL)"
                )
        );
        entity.HasKey(x => x.Id).HasName("pk_audit_entries");
        entity.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        entity.Property(x => x.OrganizationId).HasColumnName("organization_id");
        entity.Property(x => x.Action).HasColumnName("action").HasMaxLength(100);
        entity.Property(x => x.SubjectId).HasColumnName("subject_id");
        entity.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(32);
        entity.Property(x => x.ReasonCode).HasColumnName("reason_code").HasMaxLength(100);
        entity.Property(x => x.SystemActor).HasColumnName("system_actor").HasMaxLength(100);
        entity.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        entity.Property(x => x.SubjectType).HasColumnName("subject_type").HasMaxLength(100);
        entity.Property(x => x.SourceModule).HasColumnName("source_module").HasMaxLength(100);
        entity.Property(x => x.SchemaVersion).HasColumnName("schema_version");
        entity.Property(x => x.Details).HasColumnName("details").HasColumnType("jsonb");
        entity.Property(x => x.OccurredAt).HasColumnName("occurred_at");
        entity
            .HasIndex(x => new { x.OrganizationId, x.OccurredAt })
            .HasDatabaseName("ix_audit_entries_organization_occurred_at");
    }
}
