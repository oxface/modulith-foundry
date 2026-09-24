using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Modules.Sales.Persistence;

internal sealed class SalesDbContext(DbContextOptions<SalesDbContext> options) : DbContext(options)
{
    internal const string Schema = "sales";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
