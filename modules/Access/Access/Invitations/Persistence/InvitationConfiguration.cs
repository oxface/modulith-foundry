using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Access.Organizations;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Invitations.Persistence;

internal sealed class InvitationConfiguration : IEntityTypeConfiguration<Invitation>
{
    public void Configure(EntityTypeBuilder<Invitation> invitation)
    {
        invitation.ToTable("invitations");
        invitation.HasKey(entity => entity.Id).HasName("pk_invitations");
        invitation.Property(entity => entity.Id).HasColumnName("id");
        invitation.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        invitation
            .Property(entity => entity.RecipientEmail)
            .HasColumnName("recipient_email")
            .HasMaxLength(320);
        invitation
            .Property(entity => entity.SecretDigest)
            .HasColumnName("secret_digest")
            .HasColumnType("bytea")
            .HasMaxLength(32);
        invitation
            .Property(entity => entity.Generation)
            .HasColumnName("generation")
            .IsConcurrencyToken();
        invitation
            .Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsConcurrencyToken()
            .HasConversion(status => ToStoredValue(status), value => FromStoredValue(value));
        invitation.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        invitation.Property(entity => entity.ExpiresAt).HasColumnName("expires_at");
        invitation.Property(entity => entity.AcceptedByUserId).HasColumnName("accepted_by_user_id");
        invitation.Property(entity => entity.AcceptedAt).HasColumnName("accepted_at");
        invitation
            .HasIndex(entity => new { entity.OrganizationId, entity.RecipientEmail })
            .IsUnique()
            .HasFilter("status = 'pending'")
            .HasDatabaseName("ux_invitations_organization_pending_email");
        invitation
            .HasIndex(entity => entity.AcceptedByUserId)
            .HasDatabaseName("ix_invitations_accepted_by_user_id");
        invitation
            .HasAlternateKey(entity => new { entity.Id, entity.OrganizationId })
            .HasName("ak_invitations_id_organization_id");
        invitation
            .HasOne<Organization>()
            .WithMany()
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_organizations_organization_id");
        invitation
            .HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.AcceptedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_invitations_users_accepted_by_user_id");
        invitation
            .HasMany(entity => entity.RoleAssignments)
            .WithOne()
            .HasForeignKey(entity => new { entity.InvitationId, entity.OrganizationId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OrganizationId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_invitation_roles_invitations_invitation_organization");
    }

    private static string ToStoredValue(InvitationStatus status) =>
        status switch
        {
            InvitationStatus.Pending => "pending",
            InvitationStatus.Accepted => "accepted",
            _ => throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Unknown invitation status."
            ),
        };

    private static InvitationStatus FromStoredValue(string value) =>
        value switch
        {
            "pending" => InvitationStatus.Pending,
            "accepted" => InvitationStatus.Accepted,
            _ => throw new InvalidOperationException(
                $"Unknown stored invitation status '{value}'."
            ),
        };
}
