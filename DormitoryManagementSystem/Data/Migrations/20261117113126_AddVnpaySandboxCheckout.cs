using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DormitoryManagementSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddVnpaySandboxCheckout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VnpayCheckouts",
                columns: table => new
                {
                    Reference = table.Column<string>(type: "varchar(32)", unicode: false, maxLength: 32, nullable: false),
                    MerchantCode = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    InvoiceId = table.Column<long>(type: "bigint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(12,0)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    State = table.Column<string>(type: "varchar(16)", unicode: false, maxLength: 16, nullable: false),
                    ProviderTransactionNumber = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    ResponseCode = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    ProviderState = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VnpayCheckouts", x => x.Reference);
                    table.CheckConstraint("CK_VnpayCheckouts_Amount", "[Amount] BETWEEN 1 AND 9999999999");
                    table.CheckConstraint("CK_VnpayCheckouts_Expiry", "[ExpiresAtUtc] > [CreatedAtUtc]");
                    table.ForeignKey(
                        name: "FK_VnpayCheckouts_Invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "Invoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VnpayCheckouts_InvoiceId_State_ExpiresAtUtc",
                table: "VnpayCheckouts",
                columns: new[] { "InvoiceId", "State", "ExpiresAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_VnpayCheckouts_MerchantCode_ProviderTransactionNumber",
                table: "VnpayCheckouts",
                columns: new[] { "MerchantCode", "ProviderTransactionNumber" },
                unique: true,
                filter: "[ProviderTransactionNumber] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VnpayCheckouts");
        }
    }
}
