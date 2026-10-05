using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Sales;

public static class SalesDatabase
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "sales")
        );
}
