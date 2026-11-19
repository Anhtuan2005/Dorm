using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class PeakRegistrationWindows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PeakIntakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Starts = table.Column<DateOnly>(type: "date", nullable: false),
                    Ends = table.Column<DateOnly>(type: "date", nullable: false),
                    HoldHours = table.Column<int>(type: "int", nullable: false),
                    ClosedUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeakIntakes", x => x.Id);
                    table.CheckConstraint("CK_PeakIntakes_Window", "[Starts] <= [Ends] AND [HoldHours] BETWEEN 1 AND 72");
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PeakIntakes");
        }
    }
}
