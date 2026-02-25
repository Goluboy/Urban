using Microsoft.EntityFrameworkCore.Migrations;
using Urban.Domain.Geometry.Data.ValueObjects;

#nullable disable

namespace Urban.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AllowNullRenderOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<RenderOptions>(
                name: "Options",
                table: "Restrictions",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(RenderOptions),
                oldType: "jsonb");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<RenderOptions>(
                name: "Options",
                table: "Restrictions",
                type: "jsonb",
                nullable: false,
                oldClrType: typeof(RenderOptions),
                oldType: "jsonb",
                oldNullable: true);
        }
    }
}
