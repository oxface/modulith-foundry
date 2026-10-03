using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddOrderApprovalAndFulfilment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "approved_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "approved_by",
            schema: "sales",
            table: "orders",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.CreateTable(
            name: "fulfilment_processes",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_fulfilment_processes", x => x.id);
                table.ForeignKey(
                    name: "fk_fulfilment_processes_orders",
                    column: x => x.order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_fulfilment_processes_order_id",
            schema: "sales",
            table: "fulfilment_processes",
            column: "order_id"
        );

        migrationBuilder.CreateIndex(
            name: "ux_fulfilment_processes_organization_order",
            schema: "sales",
            table: "fulfilment_processes",
            columns: ["organization_id", "order_id"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "fulfilment_processes", schema: "sales");

        migrationBuilder.DropColumn(name: "approved_at", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "approved_by", schema: "sales", table: "orders");
    }
}
