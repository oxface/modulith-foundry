using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

internal sealed partial class AddOrganizations : Migration
{
    private static readonly string[] OrganizationOccurredAtColumns =
        ["organization_id", "occurred_at"];
    private static readonly string[] MembershipOrganizationUserColumns =
        ["organization_id", "user_id"];

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "organizations",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                slug = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_organizations", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "audit_entries",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                subject_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                trace_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                schema_version = table.Column<short>(type: "smallint", nullable: false),
                details = table.Column<JsonElement>(type: "jsonb", nullable: false),
                occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_audit_entries", x => x.id);
                table.ForeignKey(
                    name: "fk_audit_entries_organizations_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "access",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_audit_entries_users_actor_user_id",
                    column: x => x.actor_user_id,
                    principalSchema: "access",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "memberships",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_memberships", x => x.id);
                table.ForeignKey(
                    name: "fk_memberships_organizations_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "access",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_memberships_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "access",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "membership_role_assignments",
            schema: "access",
            columns: table => new
            {
                membership_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_membership_role_assignments", x => new { x.membership_id, x.role_id });
                table.ForeignKey(
                    name: "fk_membership_roles_memberships_membership_id",
                    column: x => x.membership_id,
                    principalSchema: "access",
                    principalTable: "memberships",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_actor_user_id",
            schema: "access",
            table: "audit_entries",
            column: "actor_user_id");

        migrationBuilder.CreateIndex(
            name: "ix_audit_entries_organization_occurred_at",
            schema: "access",
            table: "audit_entries",
            columns: OrganizationOccurredAtColumns);

        migrationBuilder.CreateIndex(
            name: "ix_memberships_user_id",
            schema: "access",
            table: "memberships",
            column: "user_id");

        migrationBuilder.CreateIndex(
            name: "ux_memberships_organization_user",
            schema: "access",
            table: "memberships",
            columns: MembershipOrganizationUserColumns,
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ux_organizations_slug",
            schema: "access",
            table: "organizations",
            column: "slug",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_entries",
            schema: "access");

        migrationBuilder.DropTable(
            name: "membership_role_assignments",
            schema: "access");

        migrationBuilder.DropTable(
            name: "memberships",
            schema: "access");

        migrationBuilder.DropTable(
            name: "organizations",
            schema: "access");
    }
}
