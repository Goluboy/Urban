using Microsoft.EntityFrameworkCore.Migrations;
using Urban.Domain.Geometry.Data.ValueObjects;

#nullable disable

namespace Urban.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FixRestrictionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GeoFeature",
                table: "GeoFeature");

            migrationBuilder.RenameTable(
                name: "GeoFeature",
                newName: "Restrictions");

            migrationBuilder.AlterColumn<RenderOptions>(
                name: "Options",
                table: "Restrictions",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(RenderOptions),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "Restrictions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(13)",
                oldMaxLength: 13);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Restrictions",
                table: "Restrictions",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_Restrictions",
                table: "Restrictions");

            migrationBuilder.RenameTable(
                name: "Restrictions",
                newName: "GeoFeature");

            migrationBuilder.AlterColumn<RenderOptions>(
                name: "Options",
                table: "GeoFeature",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(RenderOptions),
                oldType: "jsonb");

            migrationBuilder.AlterColumn<string>(
                name: "Discriminator",
                table: "GeoFeature",
                type: "character varying(13)",
                maxLength: 13,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GeoFeature",
                table: "GeoFeature",
                column: "Id");
        }
    }
}
