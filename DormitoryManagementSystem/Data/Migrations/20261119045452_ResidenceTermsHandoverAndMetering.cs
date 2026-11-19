using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class ResidenceTermsHandoverAndMetering : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE Rooms SET RoomType = CASE
                    WHEN TRIM(RoomType) COLLATE Latin1_General_100_CI_AI IN (N'Nam', N'Phòng nam') THEN N'Nam'
                    WHEN TRIM(RoomType) COLLATE Latin1_General_100_CI_AI IN (N'Nữ', N'Phòng nữ') THEN N'Nữ'
                    ELSE N'Khác' END
                WHERE TRIM(RoomType) COLLATE Latin1_General_100_CI_AI IN
                    (N'Nam', N'Nữ', N'Khác', N'Phòng nam', N'Phòng nữ', N'Phòng khác')
                """);
            migrationBuilder.CreateSequence(
                name: "RepairNumbers");

            migrationBuilder.AddColumn<string>(
                name: "ReviewEvidence",
                table: "VnpayCheckouts",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAtUtc",
                table: "VnpayCheckouts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedById",
                table: "VnpayCheckouts",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PlannedEnd",
                table: "Stays",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AppointmentUtc",
                table: "HousingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Inventory",
                table: "HousingRequests",
                type: "nvarchar(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Metered",
                table: "BillingPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ResidenceTerms",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Opens = table.Column<DateOnly>(type: "date", nullable: false),
                    Closes = table.Column<DateOnly>(type: "date", nullable: false),
                    Starts = table.Column<DateOnly>(type: "date", nullable: false),
                    Ends = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResidenceTerms", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoomMeterReadings",
                columns: table => new
                {
                    BillingPeriodId = table.Column<int>(type: "int", nullable: false),
                    RoomId = table.Column<int>(type: "int", nullable: false),
                    PowerStart = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PowerEnd = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PowerRate = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    WaterStart = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    WaterEnd = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    WaterRate = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoomMeterReadings", x => new { x.BillingPeriodId, x.RoomId });
                    table.ForeignKey(
                        name: "FK_RoomMeterReadings_BillingPeriods_BillingPeriodId",
                        column: x => x.BillingPeriodId,
                        principalTable: "BillingPeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoomMeterReadings_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StayRenewals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    StayId = table.Column<int>(type: "int", nullable: false),
                    ResidenceTermId = table.Column<int>(type: "int", nullable: false),
                    RequestedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Approved = table.Column<bool>(type: "bit", nullable: true),
                    DecidedUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedById = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StayRenewals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StayRenewals_ResidenceTerms_ResidenceTermId",
                        column: x => x.ResidenceTermId,
                        principalTable: "ResidenceTerms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StayRenewals_Stays_StayId",
                        column: x => x.StayId,
                        principalTable: "Stays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ResidenceTerms_Name",
                table: "ResidenceTerms",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RoomMeterReadings_RoomId",
                table: "RoomMeterReadings",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_StayRenewals_ResidenceTermId",
                table: "StayRenewals",
                column: "ResidenceTermId");

            migrationBuilder.CreateIndex(
                name: "IX_StayRenewals_StayId",
                table: "StayRenewals",
                column: "StayId",
                unique: true,
                filter: "[Approved] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_StayRenewals_StayId_ResidenceTermId",
                table: "StayRenewals",
                columns: new[] { "StayId", "ResidenceTermId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RoomMeterReadings");

            migrationBuilder.DropTable(
                name: "StayRenewals");

            migrationBuilder.DropTable(
                name: "ResidenceTerms");

            migrationBuilder.DropColumn(
                name: "ReviewEvidence",
                table: "VnpayCheckouts");

            migrationBuilder.DropColumn(
                name: "ReviewedAtUtc",
                table: "VnpayCheckouts");

            migrationBuilder.DropColumn(
                name: "ReviewedById",
                table: "VnpayCheckouts");

            migrationBuilder.DropColumn(
                name: "PlannedEnd",
                table: "Stays");

            migrationBuilder.DropColumn(
                name: "AppointmentUtc",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "Inventory",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "Metered",
                table: "BillingPeriods");

            migrationBuilder.DropSequence(
                name: "RepairNumbers");
        }
    }
}
