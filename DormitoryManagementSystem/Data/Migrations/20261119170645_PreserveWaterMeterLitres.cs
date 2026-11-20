using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PreserveWaterMeterLitres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "WaterStart",
                table: "RoomMeterReadings",
                type: "decimal(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);

            migrationBuilder.AlterColumn<decimal>(
                name: "WaterEnd",
                table: "RoomMeterReadings",
                type: "decimal(13,3)",
                precision: 13,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(12,2)",
                oldPrecision: 12,
                oldScale: 2);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (SELECT 1 FROM RoomMeterReadings
                           WHERE WaterStart <> ROUND(WaterStart, 2) OR WaterEnd <> ROUND(WaterEnd, 2))
                    THROW 51000, N'Chỉ số nước đang lưu đến lít. Hạ phiên bản sẽ làm mất chữ số thập phân thứ ba.', 1;
                """);
            migrationBuilder.AlterColumn<decimal>(
                name: "WaterStart",
                table: "RoomMeterReadings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,3)",
                oldPrecision: 13,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "WaterEnd",
                table: "RoomMeterReadings",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(13,3)",
                oldPrecision: 13,
                oldScale: 3);
        }
    }
}
