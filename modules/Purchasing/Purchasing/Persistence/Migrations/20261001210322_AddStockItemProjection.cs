using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Purchasing.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddStockItemProjection : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "purchasing");

        migrationBuilder.CreateTable(
            name: "stock_item_bootstrap",
            schema: "purchasing",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                is_ready = table.Column<bool>(type: "boolean", nullable: false),
                snapshot_watermark = table.Column<long>(type: "bigint", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_stock_item_bootstrap", x => x.id);
                table.CheckConstraint("ck_stock_item_bootstrap_singleton", "id = 1");
            }
        );

        migrationBuilder.CreateTable(
            name: "stock_item_references",
            schema: "purchasing",
            columns: table => new
            {
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                sku = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                description = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                base_unit_code = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                is_active = table.Column<bool>(type: "boolean", nullable: false),
                source_revision = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_stock_item_references",
                    x => new { x.organization_id, x.stock_item_id }
                );
            }
        );

        migrationBuilder.InsertData(
            schema: "purchasing",
            table: "stock_item_bootstrap",
            columns: ["id", "is_ready", "snapshot_watermark"],
            values: [1, false, null]
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "stock_item_bootstrap", schema: "purchasing");

        migrationBuilder.DropTable(name: "stock_item_references", schema: "purchasing");
    }
}
