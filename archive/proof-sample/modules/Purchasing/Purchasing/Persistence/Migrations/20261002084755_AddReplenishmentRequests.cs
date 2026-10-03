using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ModulithFoundry.Modules.Purchasing.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddReplenishmentRequests : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_entries",
            schema: "purchasing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                outcome = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                reason_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
                system_actor = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                subject_type = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                source_module = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                schema_version = table.Column<short>(type: "smallint", nullable: false),
                details = table.Column<JsonElement>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_entries", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "inbox_receipts",
            schema: "purchasing",
            columns: table => new
            {
                message_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                fingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                rejected = table.Column<bool>(type: "boolean", nullable: false),
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
            schema: "purchasing",
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
            name: "replenishment_requests",
            schema: "purchasing",
            columns: table => new
            {
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                first_message_id = table.Column<Guid>(type: "uuid", nullable: false),
                process_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_number = table.Column<long>(type: "bigint", nullable: false),
                line_number = table.Column<int>(type: "integer", nullable: false),
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
                minimum_reference_revision = table.Column<long>(type: "bigint", nullable: false),
                fingerprint = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                received_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                next_attempt_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                requirement_id = table.Column<Guid>(type: "uuid", nullable: true),
                reason_code = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_replenishment_requests", x => x.operation_id);
            }
        );

        migrationBuilder.CreateTable(
            name: "requirements",
            schema: "purchasing",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                number = table
                    .Column<long>(type: "bigint", nullable: false)
                    .Annotation(
                        "Npgsql:ValueGenerationStrategy",
                        NpgsqlValueGenerationStrategy.IdentityByDefaultColumn
                    ),
                operation_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
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
                reference_revision = table.Column<long>(type: "bigint", nullable: false),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_requirements", x => x.id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_organization_occurred_at",
            schema: "purchasing",
            table: "audit_entries",
            columns: ["organization_id", "occurred_at"]
        );

        migrationBuilder.CreateIndex(
            name: "ix_outbox_messages_pending",
            schema: "purchasing",
            table: "outbox_messages",
            column: "available_at",
            filter: "dispatched_at IS NULL"
        );

        migrationBuilder.CreateIndex(
            name: "ix_replenishment_requests_pending",
            schema: "purchasing",
            table: "replenishment_requests",
            column: "next_attempt_at",
            filter: "requirement_id IS NULL AND reason_code IS NULL"
        );

        migrationBuilder.CreateIndex(
            name: "ux_requirements_operation",
            schema: "purchasing",
            table: "requirements",
            column: "operation_id",
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ux_requirements_organization_number",
            schema: "purchasing",
            table: "requirements",
            columns: ["organization_id", "number"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_entries", schema: "purchasing");

        migrationBuilder.DropTable(name: "inbox_receipts", schema: "purchasing");

        migrationBuilder.DropTable(name: "outbox_messages", schema: "purchasing");

        migrationBuilder.DropTable(name: "replenishment_requests", schema: "purchasing");

        migrationBuilder.DropTable(name: "requirements", schema: "purchasing");
    }
}
