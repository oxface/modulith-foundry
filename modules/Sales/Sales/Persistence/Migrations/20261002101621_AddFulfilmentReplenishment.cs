using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddFulfilmentReplenishment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "replenishment_command_message_id",
            schema: "sales",
            table: "fulfilment_lines",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "replenishment_outcome_fingerprint",
            schema: "sales",
            table: "fulfilment_lines",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true
        );

        migrationBuilder.AddColumn<decimal>(
            name: "replenishment_quantity",
            schema: "sales",
            table: "fulfilment_lines",
            type: "numeric(19,6)",
            precision: 19,
            scale: 6,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "replenishment_reason_code",
            schema: "sales",
            table: "fulfilment_lines",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "replenishment_requirement_id",
            schema: "sales",
            table: "fulfilment_lines",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<long>(
            name: "replenishment_requirement_number",
            schema: "sales",
            table: "fulfilment_lines",
            type: "bigint",
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "replenishment_command_message_id",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "replenishment_outcome_fingerprint",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "replenishment_quantity",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "replenishment_reason_code",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "replenishment_requirement_id",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "replenishment_requirement_number",
            schema: "sales",
            table: "fulfilment_lines"
        );
    }
}
