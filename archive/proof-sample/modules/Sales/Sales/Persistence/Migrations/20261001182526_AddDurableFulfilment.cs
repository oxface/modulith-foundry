using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddDurableFulfilment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "sales",
            table: "order_activity",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid"
        );

        migrationBuilder.AddColumn<int>(
            name: "line_number",
            schema: "sales",
            table: "order_activity",
            type: "integer",
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "process_version",
            schema: "sales",
            table: "order_activity",
            type: "bigint",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "system_actor",
            schema: "sales",
            table: "order_activity",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "order_number",
            schema: "sales",
            table: "fulfilment_processes",
            type: "bigint",
            nullable: false,
            defaultValue: 0L
        );

        migrationBuilder.AddColumn<Guid>(
            name: "stocking_location_id",
            schema: "sales",
            table: "fulfilment_processes",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "version",
            schema: "sales",
            table: "fulfilment_processes",
            type: "bigint",
            nullable: false,
            defaultValue: 1L
        );

        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "sales",
            table: "audit_entries",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid"
        );

        migrationBuilder.AddColumn<string>(
            name: "system_actor",
            schema: "sales",
            table: "audit_entries",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "fulfilment_lines",
            schema: "sales",
            columns: table => new
            {
                line_number = table.Column<int>(type: "integer", nullable: false),
                process_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
                base_unit_code = table.Column<string>(
                    type: "character varying(16)",
                    maxLength: 16,
                    nullable: false
                ),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                command_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                reservation_id = table.Column<Guid>(type: "uuid", nullable: true),
                available_quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: true
                ),
                reason_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                outcome_fingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: true
                ),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                response_deadline = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_fulfilment_lines", x => new { x.process_id, x.line_number });
                table.ForeignKey(
                    name: "FK_fulfilment_lines_fulfilment_processes_process_id",
                    column: x => x.process_id,
                    principalSchema: "sales",
                    principalTable: "fulfilment_processes",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "inbox_receipts",
            schema: "sales",
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
            schema: "sales",
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

        migrationBuilder.CreateIndex(
            name: "ux_fulfilment_lines_command",
            schema: "sales",
            table: "fulfilment_lines",
            column: "command_message_id",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ux_fulfilment_lines_operation",
            schema: "sales",
            table: "fulfilment_lines",
            column: "operation_id",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "sales",
            table: "outbox_messages",
            column: "available_at",
            filter: "dispatched_at IS NULL"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fulfilment_lines", schema: "sales");

        migrationBuilder.DropTable(name: "inbox_receipts", schema: "sales");

        migrationBuilder.DropTable(name: "outbox_messages", schema: "sales");

        migrationBuilder.DropColumn(name: "line_number", schema: "sales", table: "order_activity");

        migrationBuilder.DropColumn(
            name: "process_version",
            schema: "sales",
            table: "order_activity"
        );

        migrationBuilder.DropColumn(name: "system_actor", schema: "sales", table: "order_activity");

        migrationBuilder.DropColumn(
            name: "order_number",
            schema: "sales",
            table: "fulfilment_processes"
        );

        migrationBuilder.DropColumn(
            name: "stocking_location_id",
            schema: "sales",
            table: "fulfilment_processes"
        );

        migrationBuilder.DropColumn(
            name: "version",
            schema: "sales",
            table: "fulfilment_processes"
        );

        migrationBuilder.DropColumn(name: "system_actor", schema: "sales", table: "audit_entries");

        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "sales",
            table: "order_activity",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true
        );

        migrationBuilder.AlterColumn<Guid>(
            name: "actor_user_id",
            schema: "sales",
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
