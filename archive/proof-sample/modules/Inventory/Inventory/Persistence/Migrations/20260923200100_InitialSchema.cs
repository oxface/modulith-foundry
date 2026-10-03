using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

[DbContext(typeof(InventoryDbContext))]
[Migration("20260923200100_InitialSchema")]
internal sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(InventoryDbContext.Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the schema because it contains this context's migration-history table.
    }
}
