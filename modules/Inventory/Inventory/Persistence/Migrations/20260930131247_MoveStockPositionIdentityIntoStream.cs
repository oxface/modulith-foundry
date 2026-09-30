using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class MoveStockPositionIdentityIntoStream : Migration
{
    private static readonly string[] StreamIdentityColumns =
        ["organization_id", "stream_type", "stocking_location_id", "stock_item_id"];
    private static readonly string[] LookupIdentityColumns =
        ["organization_id", "stocking_location_id", "stock_item_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "stock_item_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: true);
        migrationBuilder.AddColumn<Guid>(
            name: "stocking_location_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE inventory.event_streams AS stream
            SET stock_item_id = identity.stock_item_id,
                stocking_location_id = identity.stocking_location_id
            FROM inventory.stock_position_stream_identities AS identity
            WHERE identity.stream_id = stream.id
              AND identity.organization_id = stream.organization_id;
            """).Annotation("ModulithFoundry:OwnedSchema", "inventory");

        migrationBuilder.AlterColumn<Guid>(
            name: "stock_item_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(
            name: "stocking_location_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

        migrationBuilder.DropTable(name: "stock_position_stream_identities", schema: "inventory");
        migrationBuilder.CreateIndex(
            name: "ux_stock_position_stream_identity", schema: "inventory", table: "event_streams",
            columns: StreamIdentityColumns, unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_stock_position_stream_identity", schema: "inventory", table: "event_streams");
        migrationBuilder.CreateTable(
            name: "stock_position_stream_identities",
            schema: "inventory",
            columns: table => new
            {
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                stocking_location_id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stock_position_stream_identities", entity => entity.stream_id);
                table.ForeignKey(
                    name: "FK_stock_position_stream_identities_event_streams_stream_id",
                    column: entity => entity.stream_id,
                    principalSchema: "inventory", principalTable: "event_streams", principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(
            name: "ux_stock_position_stream_identity", schema: "inventory", table: "stock_position_stream_identities",
            columns: LookupIdentityColumns, unique: true);
        migrationBuilder.Sql(
            """
            INSERT INTO inventory.stock_position_stream_identities
                (stream_id, organization_id, stock_item_id, stocking_location_id)
            SELECT id, organization_id, stock_item_id, stocking_location_id
            FROM inventory.event_streams;
            """).Annotation("ModulithFoundry:OwnedSchema", "inventory");
        migrationBuilder.DropColumn(name: "stock_item_id", schema: "inventory", table: "event_streams");
        migrationBuilder.DropColumn(name: "stocking_location_id", schema: "inventory", table: "event_streams");
    }
}
