using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Modules.Purchasing.Persistence;

internal sealed class PurchasingDbContext(DbContextOptions<PurchasingDbContext> options) : DbContext(options)
{
    internal const string Schema = "purchasing";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
    }
}
