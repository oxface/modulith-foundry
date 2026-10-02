using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ModulithFoundry.Modules.Purchasing.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddPurchaseOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "system_actor",
            schema: "purchasing",
            table: "audit_entries",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100
        );
        migrationBuilder.AddCheckConstraint(
            name: "ck_audit_entries_actor",
            schema: "purchasing",
            table: "audit_entries",
            sql: "(actor_user_id IS NULL) <> (system_actor IS NULL)"
        );
        migrationBuilder.CreateTable(
            name: "event_streams",
            schema: "purchasing",
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
            schema: "purchasing",
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
                    name: "FK_events_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "purchase_order_summaries",
            schema: "purchasing",
            columns: table => new
            {
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                currency = table.Column<string>(
                    type: "character varying(3)",
                    maxLength: 3,
                    nullable: false
                ),
                is_issued = table.Column<bool>(type: "boolean", nullable: false),
                line_count = table.Column<int>(type: "integer", nullable: false),
                total = table.Column<decimal>(
                    type: "numeric(20,5)",
                    precision: 20,
                    scale: 5,
                    nullable: false
                ),
                version = table.Column<long>(type: "bigint", nullable: false),
                line_amounts = table.Column<JsonElement>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_order_summaries", x => x.stream_id);
                table.ForeignKey(
                    name: "FK_purchase_order_summaries_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "purchase_order_write_models",
            schema: "purchasing",
            columns: table => new
            {
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                version = table.Column<long>(type: "bigint", nullable: false),
                state = table.Column<JsonElement>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_purchase_order_write_models", x => x.stream_id);
                table.ForeignKey(
                    name: "FK_purchase_order_write_models_event_streams_stream_id",
                    column: x => x.stream_id,
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_event_streams_organization_type",
            schema: "purchasing",
            table: "event_streams",
            columns: ["organization_id", "stream_type"]
        );

        migrationBuilder.CreateIndex(
            name: "ux_events_global_sequence",
            schema: "purchasing",
            table: "events",
            column: "global_sequence",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ux_events_stream_version",
            schema: "purchasing",
            table: "events",
            columns: ["stream_id", "stream_version"],
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ix_purchase_order_summaries_organization_code",
            schema: "purchasing",
            table: "purchase_order_summaries",
            columns: ["organization_id", "code"]
        );

        migrationBuilder.CreateIndex(
            name: "ux_purchase_order_write_models_organization_code",
            schema: "purchasing",
            table: "purchase_order_write_models",
            columns: ["organization_id", "code"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "ck_audit_entries_actor",
            schema: "purchasing",
            table: "audit_entries"
        );
        migrationBuilder.AlterColumn<string>(
            name: "system_actor",
            schema: "purchasing",
            table: "audit_entries",
            type: "character varying(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "character varying(100)",
            oldMaxLength: 100,
            oldNullable: true
        );
        migrationBuilder.DropTable(name: "events", schema: "purchasing");

        migrationBuilder.DropTable(name: "purchase_order_summaries", schema: "purchasing");

        migrationBuilder.DropTable(name: "purchase_order_write_models", schema: "purchasing");

        migrationBuilder.DropTable(name: "event_streams", schema: "purchasing");
    }
}
