using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddStockPositions : Migration
{
    private static readonly string[] EventStreamOrganizationTypeColumns =
    [
        "organization_id",
        "stream_type",
    ];
    private static readonly string[] EventOrganizationRecordedAtColumns =
    [
        "organization_id",
        "recorded_at",
    ];
    private static readonly string[] EventStreamVersionColumns = ["stream_id", "stream_version"];
    private static readonly string[] StockPositionIdentityColumns =
    [
        "organization_id",
        "stocking_location_id",
        "stock_item_id",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "event_streams",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_type = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                version = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                updated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_event_streams", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "events",
            schema: "inventory",
            columns: table => new
            {
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
                global_sequence = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                stream_version = table.Column<long>(type: "bigint", nullable: false),
                event_name = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                schema_version = table.Column<int>(type: "integer", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                metadata = table.Column<JsonElement>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_events", x => x.event_id);
                table.ForeignKey(
                    name: "fk_events_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "inventory",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "stock_position_current",
            schema: "inventory",
            columns: table => new
            {
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                stocking_location_id = table.Column<Guid>(type: "uuid", nullable: false),
                base_unit_code = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                on_hand_quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
                reserved_quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
                available_quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
                version = table.Column<long>(type: "bigint", nullable: false),
                updated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_stock_position_current", x => x.stream_id);
                table.CheckConstraint(
                    "ck_stock_position_current_non_negative",
                    "on_hand_quantity >= 0 AND reserved_quantity >= 0 AND available_quantity >= 0"
                );
                table.CheckConstraint(
                    "ck_stock_position_current_quantity_balance",
                    "reserved_quantity <= on_hand_quantity AND available_quantity = on_hand_quantity - reserved_quantity"
                );
                table.ForeignKey(
                    name: "fk_stock_position_current_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "inventory",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_event_streams_organization_type",
            schema: "inventory",
            table: "event_streams",
            columns: EventStreamOrganizationTypeColumns
        );

        migrationBuilder.CreateIndex(
            name: "ix_events_organization_recorded_at",
            schema: "inventory",
            table: "events",
            columns: EventOrganizationRecordedAtColumns
        );

        migrationBuilder.CreateIndex(
            name: "ux_events_global_sequence",
            schema: "inventory",
            table: "events",
            column: "global_sequence",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ux_events_stream_version",
            schema: "inventory",
            table: "events",
            columns: EventStreamVersionColumns,
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ux_stock_position_current_identity",
            schema: "inventory",
            table: "stock_position_current",
            columns: StockPositionIdentityColumns,
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "events", schema: "inventory");

        migrationBuilder.DropTable(name: "stock_position_current", schema: "inventory");

        migrationBuilder.DropTable(name: "event_streams", schema: "inventory");
    }
}
