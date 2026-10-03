using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

[DbContext(typeof(AccessDbContext))]
[Migration("20260923200000_InitialSchema")]
internal sealed class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(AccessDbContext.Schema);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Keep the schema because it contains this context's migration-history table.
    }
}
