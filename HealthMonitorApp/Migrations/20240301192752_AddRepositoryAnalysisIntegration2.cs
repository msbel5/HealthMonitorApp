using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAnalysisIntegration2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Annotations",
                table: "ApiGroups",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Annotations",
                table: "ApiEndpoints",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Annotations",
                table: "ApiGroups");

            migrationBuilder.DropColumn(
                name: "Annotations",
                table: "ApiEndpoints");
        }
    }
}
