using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddDraftSalesOrders : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "order_numbers",
            schema: "sales",
            columns: table => new
            {
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                last_number = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_numbers", x => x.organization_id);
            }
        );

        migrationBuilder.CreateTable(
            name: "orders",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                customer_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_number = table.Column<long>(type: "bigint", nullable: false),
                currency = table.Column<string>(
                    type: "character varying(3)",
                    maxLength: 3,
                    nullable: false
                ),
                total_amount = table.Column<decimal>(
                    type: "numeric(19,2)",
                    precision: 19,
                    scale: 2,
                    nullable: false
                ),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_orders", x => x.id);
                table.ForeignKey(
                    name: "fk_orders_customers",
                    column: x => x.customer_id,
                    principalSchema: "sales",
                    principalTable: "customers",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "order_lines",
            schema: "sales",
            columns: table => new
            {
                line_number = table.Column<int>(type: "integer", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                stock_item_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                quantity = table.Column<decimal>(
                    type: "numeric(19,6)",
                    precision: 19,
                    scale: 6,
                    nullable: false
                ),
                unit_price = table.Column<decimal>(
                    type: "numeric(19,4)",
                    precision: 19,
                    scale: 4,
                    nullable: false
                ),
                line_amount = table.Column<decimal>(
                    type: "numeric(19,2)",
                    precision: 19,
                    scale: 2,
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_lines", x => new { x.order_id, x.line_number });
                table.ForeignKey(
                    name: "FK_order_lines_orders_order_id",
                    column: x => x.order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_orders_customer_id",
            schema: "sales",
            table: "orders",
            column: "customer_id"
        );

        migrationBuilder.CreateIndex(
            name: "ux_orders_organization_number",
            schema: "sales",
            table: "orders",
            columns: ["organization_id", "order_number"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "order_lines", schema: "sales");

        migrationBuilder.DropTable(name: "order_numbers", schema: "sales");

        migrationBuilder.DropTable(name: "orders", schema: "sales");
    }
}
