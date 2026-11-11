using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMaintenanceWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ActorUserId",
                table: "MaintenanceUpdates",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AssignedTeam",
                table: "MaintenanceRequests",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "MaintenanceRequests",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_MaintenanceUpdates_ActorUserId",
                table: "MaintenanceUpdates",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaintenanceUpdates_AspNetUsers_ActorUserId",
                table: "MaintenanceUpdates",
                column: "ActorUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaintenanceUpdates_AspNetUsers_ActorUserId",
                table: "MaintenanceUpdates");

            migrationBuilder.DropIndex(
                name: "IX_MaintenanceUpdates_ActorUserId",
                table: "MaintenanceUpdates");

            migrationBuilder.DropColumn(
                name: "ActorUserId",
                table: "MaintenanceUpdates");

            migrationBuilder.DropColumn(
                name: "AssignedTeam",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "MaintenanceRequests");
        }
    }
}
