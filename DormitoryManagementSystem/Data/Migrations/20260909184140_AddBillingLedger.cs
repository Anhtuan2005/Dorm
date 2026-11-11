using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBillingLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "ReceiptNumbers",
                startValue: 1001L);

            migrationBuilder.AddColumn<DateTime>(
                name: "LedgerUpdatedAt",
                table: "Invoices",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PaymentTransactions",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Method = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RecordedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    ReversesTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(600)", maxLength: 600, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_AspNetUsers_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PaymentTransactions_PaymentTransactions_ReversesTransactionId",
                        column: x => x.ReversesTransactionId,
                        principalTable: "PaymentTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_Code",
                table: "PaymentTransactions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_IdempotencyKey",
                table: "PaymentTransactions",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_InvoiceId",
                table: "PaymentTransactions",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_RecordedByUserId",
                table: "PaymentTransactions",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_ReversesTransactionId",
                table: "PaymentTransactions",
                column: "ReversesTransactionId",
                unique: true,
                filter: "[ReversesTransactionId] IS NOT NULL");

            migrationBuilder.Sql(
                """
                INSERT INTO [PaymentTransactions]
                    ([InvoiceId], [Code], [Amount], [Method], [ReceivedAt], [RecordedAt],
                     [RecordedByUserId], [ExternalReference], [IdempotencyKey], [ReversesTransactionId], [Note])
                SELECT [Id], CONCAT(N'THU-MIG-', [Code]), [PaidAmount], N'Adjustment', [IssuedAt], [IssuedAt],
                       NULL, N'Dữ liệu trước migration', CONCAT(N'migration-paid-', [Id]), NULL,
                       N'Số tiền đã thu được chuyển đổi từ dữ liệu cũ.'
                FROM [Invoices]
                WHERE [PaidAmount] > 0;

                UPDATE [Invoices]
                SET [Status] = N'Issued'
                WHERE [Status] <> N'Cancelled';
                """);

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Invoices");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Invoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql(
                """
                UPDATE invoiceRow
                SET [PaidAmount] = COALESCE(transactionTotals.[Collected], 0),
                    [Status] = CASE
                        WHEN invoiceRow.[Status] = N'Cancelled' THEN N'Cancelled'
                        WHEN COALESCE(transactionTotals.[Collected], 0) >= invoiceRow.[TotalAmount] THEN N'Paid'
                        WHEN COALESCE(transactionTotals.[Collected], 0) > 0 THEN N'PartiallyPaid'
                        ELSE N'Issued'
                    END
                FROM [Invoices] AS invoiceRow
                LEFT JOIN (
                    SELECT [InvoiceId], SUM([Amount]) AS [Collected]
                    FROM [PaymentTransactions]
                    GROUP BY [InvoiceId]
                ) AS transactionTotals ON transactionTotals.[InvoiceId] = invoiceRow.[Id];
                """);

            migrationBuilder.DropTable(
                name: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "LedgerUpdatedAt",
                table: "Invoices");

            migrationBuilder.DropSequence(
                name: "ReceiptNumbers");

        }
    }
}
