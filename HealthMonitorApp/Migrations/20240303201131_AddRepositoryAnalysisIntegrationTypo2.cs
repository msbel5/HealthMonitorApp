using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAnalysisIntegrationTypo2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Annotations",
                table: "ApiEndpoints",
                newName: "Annotations");

            migrationBuilder.AlterColumn<string>(
                name: "Annotations",
                table: "ApiGroups",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Annotations",
                table: "ApiEndpoints",
                newName: "Annotations");

            migrationBuilder.AlterColumn<string>(
                name: "Annotations",
                table: "ApiGroups",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");
        }
    }
}
