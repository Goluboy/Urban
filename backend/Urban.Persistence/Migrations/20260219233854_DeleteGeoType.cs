using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Urban.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DeleteGeoType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GeometryType",
                table: "GeoFeatures");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GeometryType",
                table: "GeoFeatures",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
