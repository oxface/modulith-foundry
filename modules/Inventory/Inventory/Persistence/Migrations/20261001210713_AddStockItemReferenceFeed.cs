using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddStockItemReferenceFeed : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "reference_revision",
            schema: "inventory",
            table: "stock_items",
            type: "bigint",
            nullable: false,
            defaultValue: 0L
        );

        migrationBuilder.CreateTable(
            name: "stock_item_reference_feed",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<int>(type: "integer", nullable: false),
                revision = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_stock_item_reference_feed", x => x.id);
                table.CheckConstraint(
                    "ck_stock_item_reference_feed_singleton",
                    "id = 1 AND revision >= 0"
                );
            }
        );

        migrationBuilder.InsertData(
            schema: "inventory",
            table: "stock_item_reference_feed",
            columns: ["id", "revision"],
            values: [1, 0L]
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "stock_item_reference_feed", schema: "inventory");

        migrationBuilder.DropColumn(
            name: "reference_revision",
            schema: "inventory",
            table: "stock_items"
        );
    }
}
