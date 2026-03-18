using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryPay.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddRejectionReasonToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "Payments",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "Payments");
        }
    }
}
