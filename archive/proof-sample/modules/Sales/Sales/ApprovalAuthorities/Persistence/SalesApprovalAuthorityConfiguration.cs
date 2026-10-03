using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Sales.ApprovalAuthorities.Persistence;

internal sealed class SalesApprovalAuthorityConfiguration
    : IEntityTypeConfiguration<SalesApprovalAuthority>
{
    internal const string MembershipConstraint = "ux_approval_authorities_organization_membership";

    public void Configure(EntityTypeBuilder<SalesApprovalAuthority> authority)
    {
        authority.ToTable("approval_authorities");
        authority.HasKey(entity => entity.Id).HasName("pk_approval_authorities");
        authority.Property(entity => entity.Id).HasColumnName("id").ValueGeneratedNever();
        authority.Property(entity => entity.OrganizationId).HasColumnName("organization_id");
        authority.Property(entity => entity.MembershipId).HasColumnName("membership_id");
        authority
            .Property(entity => entity.MaximumAmount)
            .HasColumnName("maximum_amount")
            .HasPrecision(19, 2);
        authority.Property(entity => entity.Currency).HasColumnName("currency").HasMaxLength(3);
        authority.Property(entity => entity.IsEnabled).HasColumnName("is_enabled");
        authority.Property(entity => entity.Version).HasColumnName("version").IsConcurrencyToken();
        authority
            .HasIndex(entity => new { entity.OrganizationId, entity.MembershipId })
            .IsUnique()
            .HasDatabaseName(MembershipConstraint);
    }
}
