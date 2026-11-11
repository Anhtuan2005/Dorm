using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveInConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests");

            migrationBuilder.AddColumn<int>(
                name: "ApprovedRoomHoldId",
                table: "HousingRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "MoveInConfirmedAt",
                table: "HousingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_ApprovedRoomHoldId",
                table: "HousingRequests",
                column: "ApprovedRoomHoldId",
                unique: true,
                filter: "[ApprovedRoomHoldId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests",
                column: "StudentProfileId",
                unique: true,
                filter: "[Status] IN (N'Submitted', N'UnderReview', N'NeedsInformation', N'AwaitingMoveIn')");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_RoomHolds_ApprovedRoomHoldId",
                table: "HousingRequests",
                column: "ApprovedRoomHoldId",
                principalTable: "RoomHolds",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_RoomHolds_ApprovedRoomHoldId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_ApprovedRoomHoldId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedRoomHoldId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "MoveInConfirmedAt",
                table: "HousingRequests");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests",
                column: "StudentProfileId",
                unique: true,
                filter: "[Status] IN (N'Submitted', N'UnderReview', N'NeedsInformation')");
        }
    }
}
