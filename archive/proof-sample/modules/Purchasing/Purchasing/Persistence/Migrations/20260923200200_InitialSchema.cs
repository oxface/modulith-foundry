using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ModulithFoundry.Modules.Purchasing.Persistence.Migrations;

[DbContext(typeof(PurchasingDbContext))]
[Migration("20260923200200_InitialSchema")]
internal sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(PurchasingDbContext.Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the schema because it contains this context's migration-history table.
    }
}
