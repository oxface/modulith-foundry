using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Inventory.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddInventoryReferenceData : Migration
{
    private static readonly string[] AuditOrganizationOccurredAtColumns =
    [
        "organization_id",
        "occurred_at",
    ];
    private static readonly string[] StockItemOrganizationActiveColumns =
    [
        "organization_id",
        "is_active",
    ];
    private static readonly string[] StockItemOrganizationSkuColumns = ["organization_id", "sku"];
    private static readonly string[] LocationOrganizationActiveColumns =
    [
        "organization_id",
        "is_active",
    ];
    private static readonly string[] LocationOrganizationCodeColumns = ["organization_id", "code"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "inventory");

        migrationBuilder.CreateTable(
            name: "audit_entries",
            schema: "inventory",
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
            name: "stock_items",
            schema: "inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                table.PrimaryKey("pk_stock_items", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "stocking_locations",
            schema: "inventory",
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
                is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                table.PrimaryKey("pk_stocking_locations", x => x.id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_organization_occurred_at",
            schema: "inventory",
            table: "audit_entries",
            columns: AuditOrganizationOccurredAtColumns
        );

        migrationBuilder.CreateIndex(
            name: "ix_stock_items_organization_active",
            schema: "inventory",
            table: "stock_items",
            columns: StockItemOrganizationActiveColumns
        );

        migrationBuilder.CreateIndex(
            name: "ux_stock_items_organization_sku",
            schema: "inventory",
            table: "stock_items",
            columns: StockItemOrganizationSkuColumns,
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ix_stocking_locations_organization_active",
            schema: "inventory",
            table: "stocking_locations",
            columns: LocationOrganizationActiveColumns
        );

        migrationBuilder.CreateIndex(
            name: "ux_stocking_locations_organization_code",
            schema: "inventory",
            table: "stocking_locations",
            columns: LocationOrganizationCodeColumns,
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "audit_entries", schema: "inventory");

        migrationBuilder.DropTable(name: "stock_items", schema: "inventory");

        migrationBuilder.DropTable(name: "stocking_locations", schema: "inventory");
    }
}
