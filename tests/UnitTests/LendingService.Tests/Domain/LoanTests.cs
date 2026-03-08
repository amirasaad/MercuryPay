using MercuryPay.LendingService.Domain;

namespace MercuryPay.LendingService.Tests.Domain;

public class LoanTests
{
    [Fact]
    public void GenerateRepaymentSchedule_ShouldCreateCorrectInstallments_ForStandardLoan()
    {
        // Arrange
        var loanId = Guid.NewGuid();
        var userId = "user-123";
        var amount = 1000m;
        var currency = "USD";
        var status = "Approved";
        var createdAt = new DateTime(2024, 1, 1);
        var termMonths = 12;
        var annualInterestRate = 0.05m; // 5%

        // Expected monthly payment: (1000 * 0.05/12 * (1+0.05/12)^12) / ((1+0.05/12)^12 - 1)
        // r = 0.0041666667
        // (1+r)^12 = 1.0511618979
        // Numerator = 1000 * 0.0041666667 * 1.0511618979 = 4.37984124
        // Denominator = 0.0511618979
        // Payment = 85.60748 -> 85.61

        // Act
        // We expect the constructor to take term and interest rate now
        var loan = new Loan(loanId, userId, amount, currency, status, createdAt, termMonths, annualInterestRate);
        
        loan.GenerateRepaymentSchedule();
        
        var schedule = loan.RepaymentSchedule;

        // Assert
        Assert.NotNull(schedule);
        Assert.Equal(12, schedule.Installments.Count);
        
        // Verify first installment
        var firstInstallment = schedule.Installments.First();
        Assert.Equal(85.61m, Math.Round(firstInstallment.TotalAmount, 2));
        
        // Verify total interest
        // Total payments = 85.607 * 12 = 1027.28
        // Total Interest = 27.28
        Assert.True(schedule.TotalInterest > 27m && schedule.TotalInterest < 28m);
        
        // Verify dates
        Assert.Equal(createdAt.AddMonths(1), firstInstallment.DueDate);
        Assert.Equal(createdAt.AddMonths(12), schedule.Installments.Last().DueDate);
    }
}
