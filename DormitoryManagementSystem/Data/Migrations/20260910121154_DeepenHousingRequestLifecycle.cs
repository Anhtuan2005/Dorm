using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeepenHousingRequestLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "HousingRequestNumbers",
                startValue: 1000L);

            migrationBuilder.AddColumn<long>(
                name: "HousingRequestId",
                table: "Stays",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Stays",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActionRequiredAt",
                table: "HousingRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceStayId",
                table: "HousingRequests",
                type: "int",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE requestRow
                SET ActionRequiredAt = COALESCE(
                    (
                        SELECT MAX(eventRow.CreatedAt)
                        FROM HousingRequestEvents AS eventRow
                        WHERE eventRow.HousingRequestId = requestRow.Id
                          AND eventRow.Status = requestRow.Status
                    ),
                    requestRow.SubmittedAt)
                FROM HousingRequests AS requestRow
                WHERE requestRow.Status IN (N'Submitted', N'UnderReview', N'NeedsInformation');

                UPDATE requestRow
                SET SourceStayId = sourceStay.Id
                FROM HousingRequests AS requestRow
                CROSS APPLY
                (
                    SELECT TOP (1) stayRow.Id
                    FROM Stays AS stayRow
                    WHERE stayRow.StudentProfileId = requestRow.StudentProfileId
                      AND stayRow.StartedAt <= requestRow.SubmittedAt
                      AND (stayRow.EndedAt IS NULL OR stayRow.EndedAt >= requestRow.SubmittedAt)
                    ORDER BY stayRow.StartedAt DESC, stayRow.Id DESC
                ) AS sourceStay
                WHERE requestRow.SourceStayId IS NULL
                  AND requestRow.Type IN (N'Transfer', N'Checkout');

                ;WITH approvedSource AS
                (
                    SELECT
                        stayRow.Id AS StayId,
                        requestRow.Id AS HousingRequestId,
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY stayRow.Id
                            ORDER BY requestRow.ReviewedAt DESC, requestRow.Id DESC
                        ) AS ChoiceNumber
                    FROM Stays AS stayRow
                    INNER JOIN HousingRequests AS requestRow
                        ON requestRow.StudentProfileId = stayRow.StudentProfileId
                       AND requestRow.TargetRoomId = stayRow.RoomId
                       AND requestRow.Status = N'Approved'
                       AND requestRow.Type IN (N'Registration', N'Transfer')
                    WHERE stayRow.HousingRequestId IS NULL
                )
                UPDATE stayRow
                SET HousingRequestId = approvedSource.HousingRequestId
                FROM Stays AS stayRow
                INNER JOIN approvedSource ON approvedSource.StayId = stayRow.Id
                WHERE approvedSource.ChoiceNumber = 1;

                INSERT INTO HousingRequestEvents (HousingRequestId, Status, Note, ActorUserId, CreatedAt)
                SELECT requestRow.Id, N'Submitted', N'Dữ liệu lịch sử được khởi tạo khi nâng cấp quy trình.', NULL, requestRow.SubmittedAt
                FROM HousingRequests AS requestRow
                WHERE NOT EXISTS
                (
                    SELECT 1
                    FROM HousingRequestEvents AS eventRow
                    WHERE eventRow.HousingRequestId = requestRow.Id
                );
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Stays_HousingRequestId",
                table: "Stays",
                column: "HousingRequestId",
                unique: true,
                filter: "[HousingRequestId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_HousingRequests_SourceStayId",
                table: "HousingRequests",
                column: "SourceStayId");

            migrationBuilder.AddForeignKey(
                name: "FK_HousingRequests_Stays_SourceStayId",
                table: "HousingRequests",
                column: "SourceStayId",
                principalTable: "Stays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Stays_HousingRequests_HousingRequestId",
                table: "Stays",
                column: "HousingRequestId",
                principalTable: "HousingRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_HousingRequests_Stays_SourceStayId",
                table: "HousingRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Stays_HousingRequests_HousingRequestId",
                table: "Stays");

            migrationBuilder.DropIndex(
                name: "IX_Stays_HousingRequestId",
                table: "Stays");

            migrationBuilder.DropIndex(
                name: "IX_HousingRequests_SourceStayId",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "HousingRequestId",
                table: "Stays");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Stays");

            migrationBuilder.DropColumn(
                name: "ActionRequiredAt",
                table: "HousingRequests");

            migrationBuilder.DropColumn(
                name: "SourceStayId",
                table: "HousingRequests");

            migrationBuilder.DropSequence(
                name: "HousingRequestNumbers");
        }
    }
}
