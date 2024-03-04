using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAnalysisIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsAuthorized",
                table: "ApiGroups",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsAuthorized",
                table: "ApiEndpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsOpen",
                table: "ApiEndpoints",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsAuthorized",
                table: "ApiGroups");

            migrationBuilder.DropColumn(
                name: "IsAuthorized",
                table: "ApiEndpoints");

            migrationBuilder.DropColumn(
                name: "IsOpen",
                table: "ApiEndpoints");
        }
    }
}
