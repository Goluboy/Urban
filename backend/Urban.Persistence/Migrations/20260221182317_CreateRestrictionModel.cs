using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CreateRestrictionModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GeoFeatures",
                table: "GeoFeatures");

            migrationBuilder.DropColumn(
                name: "AddrHouseNumber",
                table: "GeoFeatures");

            migrationBuilder.DropColumn(
                name: "AddrStreet",
                table: "GeoFeatures");

            migrationBuilder.RenameTable(
                name: "GeoFeatures",
                newName: "GeoFeature");

            migrationBuilder.AddPrimaryKey(
                name: "PK_GeoFeature",
                table: "GeoFeature",
                column: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_GeoFeature",
                table: "GeoFeature");

            migrationBuilder.RenameTable(
                name: "GeoFeature",
                newName: "GeoFeatures");

            migrationBuilder.AddColumn<string>(
                name: "AddrHouseNumber",
                table: "GeoFeatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddrStreet",
                table: "GeoFeatures",
                type: "text",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_GeoFeatures",
                table: "GeoFeatures",
                column: "Id");
        }
    }
}
