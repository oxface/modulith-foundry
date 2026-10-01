using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

internal sealed partial class AddOrganizationScope : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_membership_roles_memberships_membership_id",
            schema: "access",
            table: "membership_role_assignments"
        );

        migrationBuilder.AddColumn<Guid>(
            name: "organization_id",
            schema: "access",
            table: "membership_role_assignments",
            type: "uuid",
            nullable: true
        );

        migrationBuilder
            .Sql(
                """
                UPDATE access.membership_role_assignments AS role_assignment
                SET organization_id = membership.organization_id
                FROM access.memberships AS membership
                WHERE membership.id = role_assignment.membership_id;
                """
            )
            .Annotation("ModulithFoundry:OwnedSchema", "access");

        migrationBuilder.AlterColumn<Guid>(
            name: "organization_id",
            schema: "access",
            table: "membership_role_assignments",
            type: "uuid",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true
        );

        migrationBuilder.AddUniqueConstraint(
            name: "ak_memberships_id_organization_id",
            schema: "access",
            table: "memberships",
            columns: ["id", "organization_id"]
        );

        migrationBuilder.CreateIndex(
            name: "ix_membership_roles_membership_organization",
            schema: "access",
            table: "membership_role_assignments",
            columns: ["membership_id", "organization_id"]
        );

        migrationBuilder.CreateIndex(
            name: "ix_membership_roles_organization_membership",
            schema: "access",
            table: "membership_role_assignments",
            columns: ["organization_id", "membership_id"]
        );

        migrationBuilder.AddForeignKey(
            name: "fk_membership_roles_memberships_membership_organization",
            schema: "access",
            table: "membership_role_assignments",
            columns: ["membership_id", "organization_id"],
            principalSchema: "access",
            principalTable: "memberships",
            principalColumns: ["id", "organization_id"],
            onDelete: ReferentialAction.Cascade
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_membership_roles_memberships_membership_organization",
            schema: "access",
            table: "membership_role_assignments"
        );

        migrationBuilder.DropUniqueConstraint(
            name: "ak_memberships_id_organization_id",
            schema: "access",
            table: "memberships"
        );

        migrationBuilder.DropIndex(
            name: "ix_membership_roles_membership_organization",
            schema: "access",
            table: "membership_role_assignments"
        );

        migrationBuilder.DropIndex(
            name: "ix_membership_roles_organization_membership",
            schema: "access",
            table: "membership_role_assignments"
        );

        migrationBuilder.DropColumn(
            name: "organization_id",
            schema: "access",
            table: "membership_role_assignments"
        );

        migrationBuilder.AddForeignKey(
            name: "fk_membership_roles_memberships_membership_id",
            schema: "access",
            table: "membership_role_assignments",
            column: "membership_id",
            principalSchema: "access",
            principalTable: "memberships",
            principalColumn: "id",
            onDelete: ReferentialAction.Cascade
        );
    }
}
