using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> user)
    {
        user.ToTable("users");
        user.HasKey(entity => entity.Id).HasName("pk_users");
        user.Property(entity => entity.Id).HasColumnName("id");
        user.Property(entity => entity.Email).HasColumnName("email").HasMaxLength(320);
        user.Property(entity => entity.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        user.Property(entity => entity.CreatedAt).HasColumnName("created_at");
        user.Property(entity => entity.UpdatedAt).HasColumnName("updated_at");
    }
}
