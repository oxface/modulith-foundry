using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class UseStockPositionWriteModel : Migration
{
    private static readonly string[] StreamIdentityColumns =
        ["organization_id", "stream_type", "stocking_location_id", "stock_item_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM inventory.event_streams AS stream
                        LEFT JOIN inventory.stock_position_current AS position
                          ON position.stream_id = stream.id
                         AND position.organization_id = stream.organization_id
                        WHERE stream.stream_type = 'inventory.stock-position'
                          AND (position.stream_id IS NULL
                            OR position.version <> stream.version
                            OR position.stock_item_id <> stream.stock_item_id
                            OR position.stocking_location_id <> stream.stocking_location_id)
                    ) THEN
                        RAISE EXCEPTION 'Repair Stock Position write models before removing stream identity columns';
                    END IF;
                END;
                $$;
                """).Annotation("ModulithFoundry:OwnedSchema", "inventory");

        migrationBuilder.DropIndex(
            name: "ux_stock_position_stream_identity",
            schema: "inventory",
            table: "event_streams");

        migrationBuilder.DropColumn(
            name: "stock_item_id",
            schema: "inventory",
            table: "event_streams");

        migrationBuilder.DropColumn(
            name: "stocking_location_id",
            schema: "inventory",
            table: "event_streams");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "stock_item_id",
            schema: "inventory",
            table: "event_streams",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "stocking_location_id",
            schema: "inventory",
            table: "event_streams",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
                UPDATE inventory.event_streams AS stream
                SET stock_item_id = position.stock_item_id,
                    stocking_location_id = position.stocking_location_id
                FROM inventory.stock_position_current AS position
                WHERE position.stream_id = stream.id
                  AND position.organization_id = stream.organization_id
                  AND stream.stream_type = 'inventory.stock-position';
                """).Annotation("ModulithFoundry:OwnedSchema", "inventory");
        // The old schema cannot represent streams without this Stock Position identity.
        // Fail the downgrade instead of inventing empty business identifiers.
        migrationBuilder.AlterColumn<Guid>(
            name: "stock_item_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);
        migrationBuilder.AlterColumn<Guid>(
            name: "stocking_location_id", schema: "inventory", table: "event_streams",
            type: "uuid", nullable: false, oldClrType: typeof(Guid), oldType: "uuid", oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_stock_position_stream_identity",
            schema: "inventory",
            table: "event_streams",
            columns: StreamIdentityColumns,
            unique: true);
    }
}
