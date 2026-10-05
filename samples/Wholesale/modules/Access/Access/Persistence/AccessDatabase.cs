using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.Access.Persistence;

public static class AccessDatabase
{
    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "access")
        );
}
