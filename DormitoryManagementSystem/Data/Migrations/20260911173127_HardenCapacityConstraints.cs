using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class HardenCapacityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "CK_Stays_Chronology",
                table: "Stays",
                sql: "[EndedAt] IS NULL OR [EndedAt] >= [StartedAt]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Rooms_Capacity_Positive",
                table: "Rooms",
                sql: "[Capacity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomHolds_HeldBeds_Positive",
                table: "RoomHolds",
                sql: "[HeldBeds] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaintenanceRequests_BlockedBeds_NonNegative",
                table: "MaintenanceRequests",
                sql: "[BlockedBeds] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Stays_Chronology",
                table: "Stays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Rooms_Capacity_Positive",
                table: "Rooms");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomHolds_HeldBeds_Positive",
                table: "RoomHolds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaintenanceRequests_BlockedBeds_NonNegative",
                table: "MaintenanceRequests");
        }
    }
}
