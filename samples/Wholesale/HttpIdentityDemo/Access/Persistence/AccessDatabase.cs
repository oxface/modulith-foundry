using Microsoft.EntityFrameworkCore;

namespace ModulithFoundry.Samples.Wholesale.HttpIdentityDemo.Access.Persistence;

internal static class AccessDatabase
{
    internal static string ConnectionString(IConfiguration configuration) =>
        configuration.GetConnectionString("Access")
        ?? throw new InvalidOperationException(
            "Configure ConnectionStrings:Access for the HTTP sample."
        );

    internal static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(
            connectionString,
            postgres => postgres.MigrationsHistoryTable("__EFMigrationsHistory", "access")
        );
}
