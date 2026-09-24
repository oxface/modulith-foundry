using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.Persistence;

internal sealed class AccessAuditEntryConfiguration : IEntityTypeConfiguration<AccessAuditEntry>
{
    public void Configure(EntityTypeBuilder<AccessAuditEntry> audit)
    {
        audit.ToTable("audit_entries");
        audit.HasKey(entity => entity.Id).HasName("pk_audit_entries");
        audit.Property(entity => entity.Id).HasColumnName("id");
        audit.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        audit.Property(entity => entity.ActorUserId).HasColumnName("actor_user_id");
        audit.Property(entity => entity.Action)
            .HasColumnName("action")
            .HasMaxLength(100);
        audit.Property(entity => entity.SubjectType)
            .HasColumnName("subject_type")
            .HasMaxLength(100);
        audit.Property(entity => entity.SubjectId).HasColumnName("subject_id");
        audit.Property(entity => entity.Outcome)
            .HasColumnName("outcome")
            .HasMaxLength(32);
        audit.Property(entity => entity.ReasonCode)
            .HasColumnName("reason_code")
            .HasMaxLength(100);
        audit.Property(entity => entity.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);
        audit.Property(entity => entity.TraceId)
            .HasColumnName("trace_id")
            .HasMaxLength(32);
        audit.Property(entity => entity.SourceModule)
            .HasColumnName("source_module")
            .HasMaxLength(100);
        audit.Property(entity => entity.SchemaVersion).HasColumnName("schema_version");
        audit.Property(entity => entity.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb");
        audit.Property(entity => entity.OccurredAt).HasColumnName("occurred_at");
        audit.HasIndex(entity => new { entity.OrganizationId, entity.OccurredAt })
            .HasDatabaseName("ix_audit_entries_organization_occurred_at");
        audit.HasIndex(entity => entity.ActorUserId)
            .HasDatabaseName("ix_audit_entries_actor_user_id");
        audit.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_entries_organizations_organization_id");
        audit.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_audit_entries_users_actor_user_id");
    }
}
