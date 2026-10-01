using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddOrderSubmissionAndActivity : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "status",
            schema: "sales",
            table: "orders",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "draft"
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "submitted_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "submitted_by",
            schema: "sales",
            table: "orders",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "version",
            schema: "sales",
            table: "orders",
            type: "bigint",
            nullable: false,
            defaultValue: 1L
        );

        migrationBuilder.CreateTable(
            name: "order_activity",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                kind = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                order_version = table.Column<long>(type: "bigint", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_order_activity", x => x.id);
                table.ForeignKey(
                    name: "fk_order_activity_orders",
                    column: x => x.order_id,
                    principalSchema: "sales",
                    principalTable: "orders",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_order_activity_order_id",
            schema: "sales",
            table: "order_activity",
            column: "order_id"
        );

        migrationBuilder.CreateIndex(
            name: "ix_order_activity_organization_order_version",
            schema: "sales",
            table: "order_activity",
            columns: ["organization_id", "order_id", "order_version"]
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "order_activity", schema: "sales");

        migrationBuilder.DropColumn(name: "status", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "submitted_at", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "submitted_by", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "version", schema: "sales", table: "orders");
    }
}
