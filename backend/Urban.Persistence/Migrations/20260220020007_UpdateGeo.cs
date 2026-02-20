using Microsoft.EntityFrameworkCore.Migrations;
using Urban.Domain.Geometry.Data.ValueObjects;

#nullable disable

namespace Urban.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class UpdateGeo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Attributes",
                table: "GeoFeatures",
                newName: "Properties");

            migrationBuilder.AddColumn<RenderOptions>(
                name: "Options",
                table: "GeoFeatures",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Options",
                table: "GeoFeatures");

            migrationBuilder.RenameColumn(
                name: "Properties",
                table: "GeoFeatures",
                newName: "Attributes");
        }
    }
}
