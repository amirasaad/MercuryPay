using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MercuryPay.PaymentService.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceIdToPayment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Payments",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Payments");
        }
    }
}
