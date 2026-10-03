using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ModulithFoundry.Modules.Access.Persistence.Migrations;

internal sealed partial class AddExternalIdentities : Migration
{
    private static readonly string[] IssuerSubjectColumns = ["issuer", "subject"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "access");

        migrationBuilder.CreateTable(
            name: "users",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                email = table.Column<string>(
                    type: "character varying(320)",
                    maxLength: 320,
                    nullable: true
                ),
                display_name = table.Column<string>(
                    type: "character varying(200)",
                    maxLength: 200,
                    nullable: true
                ),
                created_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                updated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_users", x => x.id);
            }
        );

        migrationBuilder.CreateTable(
            name: "external_identities",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                issuer = table.Column<string>(
                    type: "character varying(512)",
                    maxLength: 512,
                    nullable: false
                ),
                subject = table.Column<string>(
                    type: "character varying(255)",
                    maxLength: 255,
                    nullable: false
                ),
                user_id = table.Column<Guid>(type: "uuid", nullable: false),
                linked_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
                last_authenticated_at = table.Column<DateTimeOffset>(
                    type: "timestamp with time zone",
                    nullable: false
                ),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_external_identities", x => x.id);
                table.ForeignKey(
                    name: "fk_external_identities_users_user_id",
                    column: x => x.user_id,
                    principalSchema: "access",
                    principalTable: "users",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "ix_external_identities_user_id",
            schema: "access",
            table: "external_identities",
            column: "user_id"
        );

        migrationBuilder.CreateIndex(
            name: "ux_external_identities_issuer_subject",
            schema: "access",
            table: "external_identities",
            columns: IssuerSubjectColumns,
            unique: true
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "external_identities", schema: "access");

        migrationBuilder.DropTable(name: "users", schema: "access");
    }
}
