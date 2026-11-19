using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNumberedBedsAndRoomPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BedNumber",
                table: "Stays",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BedNumber",
                table: "RoomHolds",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BlockedBedNumbers",
                table: "MaintenanceRequests",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TargetBedNumber",
                table: "HousingRequests",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RoomPhotos",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(180)", maxLength: 180, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Data = table.Column<byte[]>(type: "varbinary(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomPhotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RoomPhotos_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stays_RoomId_BedNumber",
                table: "Stays",
                columns: new[] { "RoomId", "BedNumber" },
                unique: true,
                filter: "[EndedAt] IS NULL AND [BedNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Stays_BedNumber_Positive",
                table: "Stays",
                sql: "[BedNumber] IS NULL OR [BedNumber] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RoomHolds_NumberedBed",
                table: "RoomHolds",
                sql: "[BedNumber] IS NULL OR ([BedNumber] > 0 AND [HeldBeds] = 1)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_HousingRequests_TargetBed",
                table: "HousingRequests",
                sql: "[TargetBedNumber] IS NULL OR ([TargetBedNumber] > 0 AND [TargetRoomId] IS NOT NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_RoomPhotos_RoomId",
                table: "RoomPhotos",
                column: "RoomId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomPhotos");

            migrationBuilder.DropIndex(
                name: "IX_Stays_RoomId_BedNumber",
                table: "Stays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Stays_BedNumber_Positive",
                table: "Stays");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RoomHolds_NumberedBed",
                table: "RoomHolds");

            migrationBuilder.DropCheckConstraint(
                name: "CK_HousingRequests_TargetBed",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "BedNumber",
                table: "Stays");

            migrationBuilder.DropColumn(
                name: "BedNumber",
                table: "RoomHolds");

            migrationBuilder.DropColumn(
                name: "BlockedBedNumbers",
                table: "MaintenanceRequests");

            migrationBuilder.DropColumn(
                name: "TargetBedNumber",
                table: "HousingRequests");
        }
    }
}
