using Microsoft.EntityFrameworkCore.Migrations;

namespace MercuryPay.LendingService.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Loans_UserId_CreatedAt",
                table: "Loans",
                columns: new[] { "UserId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Loans_UserId_CreatedAt",
                table: "Loans");
        }
    }
}
