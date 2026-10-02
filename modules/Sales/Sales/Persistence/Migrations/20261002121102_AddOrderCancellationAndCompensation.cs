using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Sales.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddOrderCancellationAndCompensation : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "cancellation_reason",
            schema: "sales",
            table: "orders",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "cancelled_at",
            schema: "sales",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "cancelled_by",
            schema: "sales",
            table: "orders",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<bool>(
            name: "cancellation_requested",
            schema: "sales",
            table: "fulfilment_processes",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );

        migrationBuilder.AddColumn<Guid>(
            name: "release_command_message_id",
            schema: "sales",
            table: "fulfilment_lines",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<Guid>(
            name: "release_operation_id",
            schema: "sales",
            table: "fulfilment_lines",
            type: "uuid",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "release_outcome_fingerprint",
            schema: "sales",
            table: "fulfilment_lines",
            type: "character varying(64)",
            maxLength: 64,
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "release_reason_code",
            schema: "sales",
            table: "fulfilment_lines",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "release_response_deadline",
            schema: "sales",
            table: "fulfilment_lines",
            type: "timestamp with time zone",
            nullable: true
        );

        migrationBuilder.AddColumn<string>(
            name: "release_status",
            schema: "sales",
            table: "fulfilment_lines",
            type: "character varying(32)",
            maxLength: 32,
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "cancellation_reason", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "cancelled_at", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(name: "cancelled_by", schema: "sales", table: "orders");

        migrationBuilder.DropColumn(
            name: "cancellation_requested",
            schema: "sales",
            table: "fulfilment_processes"
        );

        migrationBuilder.DropColumn(
            name: "release_command_message_id",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "release_operation_id",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "release_outcome_fingerprint",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "release_reason_code",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "release_response_deadline",
            schema: "sales",
            table: "fulfilment_lines"
        );

        migrationBuilder.DropColumn(
            name: "release_status",
            schema: "sales",
            table: "fulfilment_lines"
        );
    }
}
