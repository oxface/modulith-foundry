using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Invitations.Persistence;

internal sealed class InvitationEmailDeliveryConfiguration
    : IEntityTypeConfiguration<InvitationEmailDelivery>
{
    public void Configure(EntityTypeBuilder<InvitationEmailDelivery> delivery)
    {
        delivery.ToTable("invitation_email_deliveries");
        delivery.HasKey(entity => entity.Id).HasName("pk_invitation_email_deliveries");
        delivery.Property(entity => entity.Id).HasColumnName("id");
        delivery.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        delivery.Property(entity => entity.InvitationId).HasColumnName("invitation_id");
        delivery.Property(entity => entity.InvitationGeneration).HasColumnName("invitation_generation");
        delivery.Property(entity => entity.ProtectedPayload).HasColumnName("protected_payload");
        delivery.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        delivery.Property(entity => entity.AvailableAt).HasColumnName("available_at");
        delivery.Property(entity => entity.AttemptCount).HasColumnName("attempt_count");
        delivery.Property(entity => entity.LeaseId).HasColumnName("lease_id");
        delivery.Property(entity => entity.LeaseExpiresAt).HasColumnName("lease_expires_at");
        delivery.Property(entity => entity.SentAt).HasColumnName("sent_at");
        delivery.Property(entity => entity.SupersededAt).HasColumnName("superseded_at");
        delivery.HasIndex(entity => new
        {
            entity.AvailableAt,
            entity.SentAt,
            entity.SupersededAt,
            entity.LeaseExpiresAt,
        })
            .HasDatabaseName("ix_invitation_email_deliveries_dispatch");
        delivery.HasIndex(entity => new
        {
            entity.OrganizationId,
            entity.InvitationId,
            entity.InvitationGeneration,
        })
            .IsUnique()
            .HasDatabaseName("ux_invitation_email_deliveries_invitation_generation");
        delivery.HasIndex(entity => new { entity.InvitationId, entity.OrganizationId })
            .HasDatabaseName("ix_invitation_email_deliveries_invitation_organization");
        delivery.HasOne<Invitation>()
            .WithMany()
            .HasForeignKey(entity => new { entity.InvitationId, entity.OrganizationId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OrganizationId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitation_email_deliveries_invitation_organization");
    }
}
