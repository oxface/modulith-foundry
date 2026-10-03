using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddReservationReleaseOperations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "reservation_release_operations",
            schema: "inventory",
            columns: table => new
            {
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                outcome = table.Column<JsonElement>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_reservation_release_operations",
                    x => new { x.organization_id, x.operation_id }
                );
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "reservation_release_operations", schema: "inventory");
    }
}
