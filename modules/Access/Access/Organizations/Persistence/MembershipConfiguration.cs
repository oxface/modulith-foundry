using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ModulithFoundry.Modules.Access.Persistence;

namespace ModulithFoundry.Modules.Access.Organizations.Persistence;

internal sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> membership)
    {
        membership.ToTable("memberships");
        membership.HasKey(entity => entity.Id).HasName("pk_memberships");
        membership.Property(entity => entity.Id).HasColumnName("id");
        membership.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        membership.Property(entity => entity.UserId).HasColumnName("user_id");
        membership.Property(entity => entity.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .HasConversion(
                status => ToStoredValue(status),
                value => FromStoredValue(value));
        membership.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        membership.HasIndex(entity => new { entity.OrganizationId, entity.UserId })
            .IsUnique()
            .HasDatabaseName("ux_memberships_organization_user");
        membership.HasIndex(entity => entity.UserId)
            .HasDatabaseName("ix_memberships_user_id");
        membership.HasAlternateKey(entity => new { entity.Id, entity.OrganizationId })
            .HasName("ak_memberships_id_organization_id");
        membership.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_memberships_organizations_organization_id");
        membership.HasOne<User>()
            .WithMany()
            .HasForeignKey(entity => entity.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_memberships_users_user_id");
        membership.HasMany(entity => entity.RoleAssignments)
            .WithOne()
            .HasForeignKey(entity => new { entity.MembershipId, entity.OrganizationId })
            .HasPrincipalKey(entity => new { entity.Id, entity.OrganizationId })
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_membership_roles_memberships_membership_organization");
    }

    private static string ToStoredValue(MembershipStatus status) =>
        status switch
        {
            MembershipStatus.Active => "active",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown membership status."),
        };

    private static MembershipStatus FromStoredValue(string value) =>
        value switch
        {
            "active" => MembershipStatus.Active,
            _ => throw new InvalidOperationException($"Unknown stored membership status '{value}'."),
        };
}
