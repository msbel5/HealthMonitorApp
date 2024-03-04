using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryAnalysisUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CodeQualityScore",
                table: "RepositoryAnalyses");

            migrationBuilder.RenameColumn(
                name: "NumberOfPublicControllers",
                table: "RepositoryAnalyses",
                newName: "NumberOfSecurityIssues");

            migrationBuilder.AddColumn<string>(
                name: "BaseUrl",
                table: "RepositoryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Branch",
                table: "RepositoryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LatestCommitHash",
                table: "RepositoryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "RepositoryAnalyses",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "NumberOfEndpoints",
                table: "RepositoryAnalyses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfFeatures",
                table: "RepositoryAnalyses",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "NumberOfPublicEndpoints",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUrl",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "Branch",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "LatestCommitHash",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "NumberOfEndpoints",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "NumberOfFeatures",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "NumberOfPublicEndpoints",
                table: "RepositoryAnalyses");

            migrationBuilder.DropColumn(
                name: "RawAnalysis",
                table: "RepositoryAnalyses");

            migrationBuilder.RenameColumn(
                name: "NumberOfSecurityIssues",
                table: "RepositoryAnalyses",
                newName: "NumberOfPublicControllers");

            migrationBuilder.AddColumn<double>(
                name: "CodeQualityScore",
                table: "RepositoryAnalyses",
                type: "REAL",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
