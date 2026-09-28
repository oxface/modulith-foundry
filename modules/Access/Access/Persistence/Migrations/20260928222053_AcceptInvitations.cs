using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AcceptInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "accepted_at",
            schema: "access",
            table: "invitations",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "accepted_by_user_id",
            schema: "access",
            table: "invitations",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_invitations_accepted_by_user_id",
            schema: "access",
            table: "invitations",
            column: "accepted_by_user_id");

        migrationBuilder.AddForeignKey(
            name: "fk_invitations_users_accepted_by_user_id",
            schema: "access",
            table: "invitations",
            column: "accepted_by_user_id",
            principalSchema: "access",
            principalTable: "users",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_invitations_users_accepted_by_user_id",
            schema: "access",
            table: "invitations");

        migrationBuilder.DropIndex(
            name: "ix_invitations_accepted_by_user_id",
            schema: "access",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "accepted_at",
            schema: "access",
            table: "invitations");

        migrationBuilder.DropColumn(
            name: "accepted_by_user_id",
            schema: "access",
            table: "invitations");
    }
}
