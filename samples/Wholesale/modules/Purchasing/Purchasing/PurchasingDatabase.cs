using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Purchasing;

public static class PurchasingDatabase
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "purchasing")
        );
}
