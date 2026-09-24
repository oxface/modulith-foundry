using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Organizations.Persistence;

internal sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> organization)
    {
        organization.ToTable("organizations");
        organization.HasKey(entity => entity.Id).HasName("pk_organizations");
        organization.Property(entity => entity.Id).HasColumnName("id");
        organization.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(200);
        organization.Property(entity => entity.Slug)
            .HasColumnName("slug")
            .HasMaxLength(63)
            .HasConversion(
                slug => slug.Value,
                value => OrganizationSlug.Create(value));
        organization.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        organization.HasIndex(entity => entity.Slug)
            .IsUnique()
            .HasDatabaseName("ux_organizations_slug");
    }
}
