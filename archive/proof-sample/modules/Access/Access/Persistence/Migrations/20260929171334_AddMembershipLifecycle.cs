using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

/// <inheritdoc />
internal sealed partial class AddMembershipLifecycle : Migration
{
    private static readonly string[] MembershipOrganizationUserColumns =
    [
        "organization_id",
        "user_id",
    ];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_memberships_organization_user",
            schema: "access",
            table: "memberships"
        );

        migrationBuilder.CreateIndex(
            name: "ux_memberships_organization_user",
            schema: "access",
            table: "memberships",
            columns: MembershipOrganizationUserColumns,
            unique: true,
            filter: "status IN ('active', 'suspended')"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_memberships_organization_user",
            schema: "access",
            table: "memberships"
        );

        migrationBuilder.CreateIndex(
            name: "ux_memberships_organization_user",
            schema: "access",
            table: "memberships",
            columns: MembershipOrganizationUserColumns,
            unique: true
        );
    }
}
