using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Samples.Wholesale.Purchasing.Migrations;

/// <inheritdoc />
public partial class InitialPurchasing : Migration
{
    private static readonly string[] StreamKeyColumns = ["organization_key", "id"];
    private static readonly string[] PositionColumns =
    [
        "organization_key",
        "stream_id",
        "stream_version",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "purchasing");

        migrationBuilder.CreateTable(
            name: "event_streams",
            schema: "purchasing",
            columns: table => new
            {
                organization_key = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false
                ),
                id = table.Column<Guid>(type: "uuid", nullable: false),
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
                table.PrimaryKey("PK_event_streams", x => new { x.organization_key, x.id });
                table.CheckConstraint("positive_stream_version", "version >= 1");
            }
        );

        migrationBuilder.CreateTable(
            name: "events",
            schema: "purchasing",
            columns: table => new
            {
                organization_key = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false
                ),
                event_id = table.Column<Guid>(type: "uuid", nullable: false),
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
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_events", x => new { x.organization_key, x.event_id });
                table.CheckConstraint("positive_event_schema", "schema_version >= 1");
                table.CheckConstraint("positive_event_version", "stream_version >= 1");
                table.ForeignKey(
                    name: "FK_events_event_streams_organization_key_stream_id",
                    columns: x => new { x.organization_key, x.stream_id },
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumns: StreamKeyColumns,
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_events_organization_key_stream_id_stream_version",
            schema: "purchasing",
            table: "events",
            columns: PositionColumns,
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "events", schema: "purchasing");

        migrationBuilder.DropTable(name: "event_streams", schema: "purchasing");
    }
}
