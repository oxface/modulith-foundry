using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddCustomers : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "sales");

        migrationBuilder.CreateTable(
            name: "audit_entries",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                subject_type = table.Column<string>(
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
            name: "customers",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(
                    type: "character varying(64)",
                    maxLength: 64,
                    nullable: false
                ),
                name = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: false
                ),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_customers", x => x.id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_organization_occurred_at",
            schema: "sales",
            table: "audit_entries",
            columns: ["organization_id", "occurred_at"]
        );

        migrationBuilder.CreateIndex(
            name: "ux_customers_organization_code",
            schema: "sales",
            table: "customers",
            columns: ["organization_id", "code"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_entries", schema: "sales");

        migrationBuilder.DropTable(name: "customers", schema: "sales");
    }
}
