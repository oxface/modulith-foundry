using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

[DbContext(typeof(SalesDbContext))]
[Migration("20260923200300_InitialSchema")]
internal sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(SalesDbContext.Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the schema because it contains this context's migration-history table.
    }
}
