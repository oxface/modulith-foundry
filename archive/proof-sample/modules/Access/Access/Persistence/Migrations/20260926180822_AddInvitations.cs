using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // EF Core generates array arguments for composite keys and indexes.

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

internal sealed partial class AddInvitations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "invitations",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                recipient_email = table.Column<string>(
                    type: "character varying(320)",
                    maxLength: 320,
                    nullable: false
                ),
                secret_digest = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                generation = table.Column<int>(type: "integer", nullable: false),
                status = table.Column<string>(
                    type: "character varying(32)",
                    maxLength: 32,
                    nullable: false
                ),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                expires_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_invitations", x => x.id);
                table.UniqueConstraint(
                    "ak_invitations_id_organization_id",
                    x => new { x.id, x.organization_id }
                );
                table.ForeignKey(
                    name: "fk_invitations_organizations_organization_id",
                    column: x => x.organization_id,
                    principalSchema: "access",
                    principalTable: "organizations",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "invitation_email_deliveries",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                invitation_generation = table.Column<int>(type: "integer", nullable: false),
                protected_payload = table.Column<string>(type: "text", nullable: true),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                available_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                attempt_count = table.Column<int>(type: "integer", nullable: false),
                lease_id = table.Column<Guid>(type: "uuid", nullable: true),
                lease_expires_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                sent_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
                superseded_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: true
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_invitation_email_deliveries", x => x.id);
                table.ForeignKey(
                    name: "fk_invitation_email_deliveries_invitation_organization",
                    columns: x => new { x.invitation_id, x.organization_id },
                    principalSchema: "access",
                    principalTable: "invitations",
                    principalColumns: new[] { "id", "organization_id" },
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateTable(
            name: "invitation_role_assignments",
            schema: "access",
            columns: table => new
            {
                invitation_id = table.Column<Guid>(type: "uuid", nullable: false),
                role_id = table.Column<string>(
                    type: "character varying(100)",
                    maxLength: 100,
                    nullable: false
                ),
                organization_id = table.Column<Guid>(type: "uuid", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "pk_invitation_role_assignments",
                    x => new { x.invitation_id, x.role_id }
                );
                table.ForeignKey(
                    name: "fk_invitation_roles_invitations_invitation_organization",
                    columns: x => new { x.invitation_id, x.organization_id },
                    principalSchema: "access",
                    principalTable: "invitations",
                    principalColumns: new[] { "id", "organization_id" },
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_invitation_email_deliveries_dispatch",
            schema: "access",
            table: "invitation_email_deliveries",
            columns: new[] { "available_at", "sent_at", "superseded_at", "lease_expires_at" }
        );

        migrationBuilder.CreateIndex(
            name: "ix_invitation_email_deliveries_invitation_organization",
            schema: "access",
            table: "invitation_email_deliveries",
            columns: new[] { "invitation_id", "organization_id" }
        );

        migrationBuilder.CreateIndex(
            name: "ux_invitation_email_deliveries_invitation_generation",
            schema: "access",
            table: "invitation_email_deliveries",
            columns: new[] { "organization_id", "invitation_id", "invitation_generation" },
            unique: true
        );

        migrationBuilder.CreateIndex(
            name: "ix_invitation_roles_invitation_organization",
            schema: "access",
            table: "invitation_role_assignments",
            columns: new[] { "invitation_id", "organization_id" }
        );

        migrationBuilder.CreateIndex(
            name: "ix_invitation_roles_organization_invitation",
            schema: "access",
            table: "invitation_role_assignments",
            columns: new[] { "organization_id", "invitation_id" }
        );

        migrationBuilder.CreateIndex(
            name: "ux_invitations_organization_pending_email",
            schema: "access",
            table: "invitations",
            columns: new[] { "organization_id", "recipient_email" },
            unique: true,
            filter: "status = 'pending'"
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "invitation_email_deliveries", schema: "access");

        migrationBuilder.DropTable(name: "invitation_role_assignments", schema: "access");

        migrationBuilder.DropTable(name: "invitations", schema: "access");
    }
}
