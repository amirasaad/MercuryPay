using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryPay.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnReferenceId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReferenceId",
                table: "Payments",
                column: "ReferenceId",
                unique: true,
                filter: "\"ReferenceId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ReferenceId",
                table: "Payments");
        }
    }
}
