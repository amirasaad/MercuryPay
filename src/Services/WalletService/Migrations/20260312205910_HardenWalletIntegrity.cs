using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryPay.WalletService.Migrations
{
    /// <inheritdoc />
    public partial class HardenWalletIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_WalletId",
                table: "LedgerEntries");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Wallets",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_Wallets_UserId_Currency",
                table: "Wallets",
                columns: new[] { "UserId", "Currency" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_WalletId_TransactionId",
                table: "LedgerEntries",
                columns: new[] { "WalletId", "TransactionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Wallets_UserId_Currency",
                table: "Wallets");

            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_WalletId_TransactionId",
                table: "LedgerEntries");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Wallets");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_WalletId",
                table: "LedgerEntries",
                column: "WalletId");
        }
    }
}
