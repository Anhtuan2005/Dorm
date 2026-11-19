using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceFollowers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShareWithRoom",
                table: "MaintenanceRequests",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MaintenanceFollowers",
                columns: table => new
                {
                    MaintenanceRequestId = table.Column<int>(type: "int", nullable: false),
                    StayId = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaintenanceFollowers", x => new { x.MaintenanceRequestId, x.StayId });
                    table.ForeignKey(
                        name: "FK_MaintenanceFollowers_MaintenanceRequests_MaintenanceRequestId",
                        column: x => x.MaintenanceRequestId,
                        principalTable: "MaintenanceRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MaintenanceFollowers_Stays_StayId",
                        column: x => x.StayId,
                        principalTable: "Stays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceFollowers_StayId",
                table: "MaintenanceFollowers",
                column: "StayId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaintenanceFollowers");

            migrationBuilder.DropColumn(
                name: "ShareWithRoom",
                table: "MaintenanceRequests");
        }
    }
}
