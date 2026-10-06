using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Samples.Wholesale.Purchasing.Migrations;

/// <inheritdoc />
public partial class AddPurchaseOrderViews : Migration
{
    private static readonly string[] HeaderColumns = ["organization_key", "id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "purchase_order_current",
            schema: "purchasing",
            columns: table => new
            {
                organization_key = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false
                ),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                state = table.Column<JsonElement>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_purchase_order_current",
                    x => new { x.organization_key, x.stream_id }
                );
                table.CheckConstraint("ck_purchase_order_current_version", "version > 0");
                table.ForeignKey(
                    name: "FK_purchase_order_current_event_streams_organization_key_strea~",
                    columns: x => new { x.organization_key, x.stream_id },
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumns: HeaderColumns,
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "purchase_order_summary",
            schema: "purchasing",
            columns: table => new
            {
                organization_key = table.Column<string>(
                    type: "character varying(256)",
                    maxLength: 256,
                    nullable: false
                ),
                stream_id = table.Column<Guid>(type: "uuid", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
                recorded_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                code = table.Column<string>(type: "text", nullable: false),
                currency = table.Column<string>(type: "text", nullable: false),
                line_count = table.Column<int>(type: "integer", nullable: false),
                total = table.Column<decimal>(type: "numeric", nullable: false),
                line_amounts = table.Column<JsonElement>(type: "jsonb", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_purchase_order_summary",
                    x => new { x.organization_key, x.stream_id }
                );
                table.CheckConstraint(
                    "ck_purchase_order_summary_amounts",
                    "line_count >= 0 AND total >= 0"
                );
                table.CheckConstraint("ck_purchase_order_summary_version", "version > 0");
                table.ForeignKey(
                    name: "FK_purchase_order_summary_event_streams_organization_key_strea~",
                    columns: x => new { x.organization_key, x.stream_id },
                    principalSchema: "purchasing",
                    principalTable: "event_streams",
                    principalColumns: HeaderColumns,
                    onDelete: ReferentialAction.Restrict
                );
            }
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "purchase_order_current", schema: "purchasing");

        migrationBuilder.DropTable(name: "purchase_order_summary", schema: "purchasing");
    }
}
