using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Modules.Access.Persistence;

internal sealed class AccessDbContext(DbContextOptions<AccessDbContext> options) : DbContext(options)
{
    internal const string Schema = "access";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
