using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class repositoryAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "RepositoryAnalysisId",
                table: "ApiGroups",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RepositoryAnalyses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedUsername = table.Column<string>(type: "TEXT", nullable: false),
                    EncryptedPassword = table.Column<string>(type: "TEXT", nullable: false),
                    Path = table.Column<string>(type: "TEXT", nullable: false),
                    NumberOfControllers = table.Column<int>(type: "INTEGER", nullable: false),
                    NumberOfPublicControllers = table.Column<int>(type: "INTEGER", nullable: false),
                    CodeQualityScore = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryAnalyses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiGroups_RepositoryAnalysisId",
                table: "ApiGroups",
                column: "RepositoryAnalysisId");

            migrationBuilder.AddForeignKey(
                name: "FK_ApiGroups_RepositoryAnalyses_RepositoryAnalysisId",
                table: "ApiGroups",
                column: "RepositoryAnalysisId",
                principalTable: "RepositoryAnalyses",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ApiGroups_RepositoryAnalyses_RepositoryAnalysisId",
                table: "ApiGroups");

            migrationBuilder.DropTable(
                name: "RepositoryAnalyses");

            migrationBuilder.DropIndex(
                name: "IX_ApiGroups_RepositoryAnalysisId",
                table: "ApiGroups");

            migrationBuilder.DropColumn(
                name: "RepositoryAnalysisId",
                table: "ApiGroups");
        }
    }
}
