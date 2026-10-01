using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Invitations.Persistence;

internal sealed class InvitationRoleAssignmentConfiguration
    : IEntityTypeConfiguration<InvitationRoleAssignment>
{
    public void Configure(EntityTypeBuilder<InvitationRoleAssignment> role)
    {
        role.ToTable("invitation_role_assignments");
        role.HasKey(entity => new { entity.InvitationId, entity.RoleId })
            .HasName("pk_invitation_role_assignments");
        role.Property(entity => entity.InvitationId).HasColumnName("invitation_id");
        role.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        role.Property(entity => entity.RoleId).HasColumnName("role_id").HasMaxLength(100);
        role.HasIndex(entity => new { entity.OrganizationId, entity.InvitationId })
            .HasDatabaseName("ix_invitation_roles_organization_invitation");
        role.HasIndex(entity => new { entity.InvitationId, entity.OrganizationId })
            .HasDatabaseName("ix_invitation_roles_invitation_organization");
    }
}
