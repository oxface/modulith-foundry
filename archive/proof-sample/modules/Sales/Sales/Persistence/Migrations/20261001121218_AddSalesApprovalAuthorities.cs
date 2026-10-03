using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddSalesApprovalAuthorities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "approval_authorities",
            schema: "sales",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                maximum_amount = table.Column<decimal>(
                    type: "numeric(19,2)",
                    precision: 19,
                    scale: 2,
                    nullable: false
                ),
                currency = table.Column<string>(
                    type: "character varying(3)",
                    maxLength: 3,
                    nullable: false
                ),
                is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                version = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_approval_authorities", x => x.id);
            }
        );

        migrationBuilder.CreateIndex(
            name: "ux_approval_authorities_organization_membership",
            schema: "sales",
            table: "approval_authorities",
            columns: ["organization_id", "membership_id"],
            unique: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "approval_authorities", schema: "sales");
    }
}
