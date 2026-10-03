using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddDurableReservations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<JsonElement>(
            name: "reservations",
            schema: "inventory",
            table: "stock_position_current",
            type: "jsonb",
            nullable: false,
            defaultValueSql: "'[]'::jsonb"
        );

        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "inventory",
            table: "audit_entries",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid"
        );

        migrationBuilder.AddColumn<string>(
            name: "system_actor",
            schema: "inventory",
            table: "audit_entries",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "inbox_receipts",
            schema: "inventory",
            columns: table => new
            {
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                processed_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_inbox_receipts", x => x.message_id);
            }
        );

        migrationBuilder.CreateTable(
            name: "outbox_messages",
            schema: "inventory",
            columns: table => new
            {
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                message_type = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                payload = table.Column<JsonElement>(type: "jsonb", nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                available_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                dispatched_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                lease_token = table.Column<Guid>(type: "uuid", nullable: true),
                lease_until = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                attempts = table.Column<int>(type: "integer", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_outbox_messages", x => x.message_id);
            }
        );

        migrationBuilder.CreateTable(
            name: "reservation_operations",
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
                    "pk_reservation_operations",
                    x => new { x.organization_id, x.operation_id }
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "inventory",
            table: "outbox_messages",
            column: "available_at",
            filter: "dispatched_at IS NULL"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "inbox_receipts", schema: "inventory");

        migrationBuilder.DropTable(name: "outbox_messages", schema: "inventory");

        migrationBuilder.DropTable(name: "reservation_operations", schema: "inventory");

        migrationBuilder.DropColumn(
            name: "reservations",
            schema: "inventory",
            table: "stock_position_current"
        );

        migrationBuilder.DropColumn(
            name: "system_actor",
            schema: "inventory",
            table: "audit_entries"
        );

        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "inventory",
            table: "audit_entries",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true
        );
    }
}
