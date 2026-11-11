using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MaintenanceAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    StoredName = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    OriginalName = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaintenanceAttachments_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachments_MaintenanceRequestId",
                table: "MaintenanceAttachments",
                column: "MaintenanceRequestId");

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceAttachments_StoredName",
                table: "MaintenanceAttachments",
                column: "StoredName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceAttachments");
        }
    }
}
