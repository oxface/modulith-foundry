using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class SeparateStockPositionStreamIdentity : Migration
{
    private static readonly string[] IdentityColumns =
        ["organization_id", "stocking_location_id", "stock_item_id"];
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "stock_position_stream_identities",
            schema: "inventory",
            columns: table => new
            {
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                stocking_location_id = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stock_position_stream_identities", x => x.stream_id);
                table.ForeignKey(
                    name: "FK_stock_position_stream_identities_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "inventory",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "ux_stock_position_stream_identity",
            schema: "inventory",
            table: "stock_position_stream_identities",
            columns: IdentityColumns,
            unique: true);

        migrationBuilder.Sql(
            """
                INSERT INTO inventory.stock_position_stream_identities
                    (stream_id, organization_id, stock_item_id, stocking_location_id)
                SELECT stream_id, organization_id, stock_item_id, stocking_location_id
                FROM inventory.stock_position_current;
                """).Annotation("ModulithFoundry:OwnedSchema", "inventory");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "stock_position_stream_identities",
            schema: "inventory");
    }
}
