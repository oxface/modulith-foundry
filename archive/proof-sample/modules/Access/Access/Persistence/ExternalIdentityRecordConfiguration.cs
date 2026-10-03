using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class ExternalIdentityRecordConfiguration
    : IEntityTypeConfiguration<ExternalIdentityRecord>
{
    public void Configure(EntityTypeBuilder<ExternalIdentityRecord> identity)
    {
        identity.ToTable("external_identities");
        identity.HasKey(entity => entity.Id).HasName("pk_external_identities");
        identity.Property(entity => entity.Id).HasColumnName("id");
        identity.Property(entity => entity.Issuer).HasColumnName("issuer").HasMaxLength(512);
        identity.Property(entity => entity.Subject).HasColumnName("subject").HasMaxLength(255);
        identity.Property(entity => entity.UserId).HasColumnName("user_id");
        identity.Property(entity => entity.LinkedAt).HasColumnName("linked_at");
        identity
            .Property(entity => entity.LastAuthenticatedAt)
            .HasColumnName("last_authenticated_at");
        identity
            .HasIndex(entity => new { entity.Issuer, entity.Subject })
            .IsUnique()
            .HasDatabaseName(ExternalIdentityRecord.IssuerSubjectConstraint);
        identity
            .HasIndex(entity => entity.UserId)
            .HasDatabaseName("ix_external_identities_user_id");
        identity
            .HasOne(entity => entity.User)
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_external_identities_users_user_id");
    }
}
