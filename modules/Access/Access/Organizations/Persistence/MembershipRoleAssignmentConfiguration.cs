using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Organizations.Persistence;

internal sealed class MembershipRoleAssignmentConfiguration
    : IEntityTypeConfiguration<MembershipRoleAssignment>
{
    public void Configure(EntityTypeBuilder<MembershipRoleAssignment> role)
    {
        role.ToTable("membership_role_assignments");
        role.HasKey(entity => new { entity.MembershipId, entity.RoleId })
            .HasName("pk_membership_role_assignments");
        role.Property(entity => entity.MembershipId).HasColumnName("membership_id");
        role.Property(entity => entity.RoleId)
            .HasColumnName("role_id")
            .HasMaxLength(100);
        role.Property(entity => entity.AssignedAt).HasColumnName("assigned_at");
    }
}
