using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAnalysisUpdate3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumberOfFeatures",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "NumberOfSecurityIssues",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "RawAnalysis",
                table: "RepositoryAnalyses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumberOfFeatures",
                table: "RepositoryAnalyses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfSecurityIssues",
                table: "RepositoryAnalyses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RawAnalysis",
                table: "RepositoryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
