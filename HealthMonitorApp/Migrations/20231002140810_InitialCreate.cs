using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HealthMonitorApp.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiGroups",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiGroups", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStatuses",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTime = table.Column<double>(type: "REAL", nullable: false),
                    ResponseContent = table.Column<string>(type: "TEXT", nullable: true),
                    ApiEndpointID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStatuses", x => x.ID);
                });

            migrationBuilder.CreateTable(
                name: "ApiEndpoints",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    cURL = table.Column<string>(type: "TEXT", nullable: false),
                    ExpectedStatusCode = table.Column<int>(type: "INTEGER", nullable: false),
                    ApiGroupID = table.Column<int>(type: "INTEGER", nullable: false),
                    ServiceStatusID = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiEndpoints", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ApiEndpoints_ApiGroups_ApiGroupID",
                        column: x => x.ApiGroupID,
                        principalTable: "ApiGroups",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ApiEndpoints_ServiceStatuses_ServiceStatusID",
                        column: x => x.ServiceStatusID,
                        principalTable: "ServiceStatuses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceStatusHistories",
                columns: table => new
                {
                    ID = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ServiceStatusID = table.Column<int>(type: "INTEGER", nullable: false),
                    IsHealthy = table.Column<bool>(type: "INTEGER", nullable: false),
                    CheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ResponseTime = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceStatusHistories", x => x.ID);
                    table.ForeignKey(
                        name: "FK_ServiceStatusHistories_ServiceStatuses_ServiceStatusID",
                        column: x => x.ServiceStatusID,
                        principalTable: "ServiceStatuses",
                        principalColumn: "ID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpoints_ApiGroupID",
                table: "ApiEndpoints",
                column: "ApiGroupID");

            migrationBuilder.CreateIndex(
                name: "IX_ApiEndpoints_ServiceStatusID",
                table: "ApiEndpoints",
                column: "ServiceStatusID",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServiceStatusHistories_ServiceStatusID",
                table: "ServiceStatusHistories",
                column: "ServiceStatusID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiEndpoints");

            migrationBuilder.DropTable(
                name: "ServiceStatusHistories");

            migrationBuilder.DropTable(
                name: "ApiGroups");

            migrationBuilder.DropTable(
                name: "ServiceStatuses");
        }
    }
}
