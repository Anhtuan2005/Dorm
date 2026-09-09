using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddApplicationWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests");

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "HousingRequests",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HousingRequestEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HousingRequestId = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingRequestEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HousingRequestEvents_AspNetUsers_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HousingRequestEvents_HousingRequests_HousingRequestId",
                        column: x => x.HousingRequestId,
                        principalTable: "HousingRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_ReviewedByUserId",
                table: "HousingRequests",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests",
                column: "StudentProfileId",
                unique: true,
                filter: "[Status] IN (N'Submitted', N'UnderReview', N'NeedsInformation')");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequestEvents_ActorUserId",
                table: "HousingRequestEvents",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequestEvents_HousingRequestId",
                table: "HousingRequestEvents",
                column: "HousingRequestId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_AspNetUsers_ReviewedByUserId",
                table: "HousingRequests",
                column: "ReviewedByUserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_AspNetUsers_ReviewedByUserId",
                table: "HousingRequests");

            migrationBuilder.DropTable(
                name: "HousingRequestEvents");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_ReviewedByUserId",
                table: "HousingRequests");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "HousingRequests");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_StudentProfileId",
                table: "HousingRequests",
                column: "StudentProfileId",
                unique: true,
                filter: "[Status] IN (N'Submitted', N'UnderReview')");
        }
    }
}
