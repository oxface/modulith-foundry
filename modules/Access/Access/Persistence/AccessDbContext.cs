using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    internal const string Schema = "access";

    internal DbSet<User> Users => Set<User>();

    internal DbSet<ExternalIdentityRecord> ExternalIdentities => Set<ExternalIdentityRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AccessDbContext).Assembly);
    }
}
