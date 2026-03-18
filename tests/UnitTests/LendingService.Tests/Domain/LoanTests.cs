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

    [Fact]
    public void MarkAsFraudDetected_ShouldCancelAllPendingInstallments()
    {
        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
        loan.GenerateRepaymentSchedule();
        loan.MarkAsFraudDetected();
        Assert.All(loan.RepaymentSchedule!.Installments, i =>
            Assert.True(i.Status == "Cancelled" || i.Status == "Paid"));
        Assert.Equal("FraudDetected", loan.Status);
    }

    [Fact]
    public void Loan_WhenAmountExceedsMaximum_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 100001m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m));
    }

    [Fact]
    public void ProcessRepayment_ShouldHandlePartialPayments()
    {
        // Arrange
        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
        loan.GenerateRepaymentSchedule();
        var firstInstallment = loan.RepaymentSchedule!.Installments.First();
        var paymentAmount = firstInstallment.TotalAmount / 2;

        // Act
        loan.ProcessRepayment(paymentAmount);

        // Assert
        var updatedInstallment = loan.RepaymentSchedule.Installments.First();
        Assert.Equal("PartiallyPaid", updatedInstallment.Status);
        Assert.Equal(paymentAmount, updatedInstallment.PaidAmount);
        Assert.Equal("Active", loan.Status);
    }

    [Fact]
    public void ProcessRepayment_ShouldHandleFullRepayment()
    {
        // Arrange
        var loan = new Loan(Guid.NewGuid(), "user-1", 100m, "USD", "Approved", DateTime.UtcNow, 1, 0.05m);
        loan.GenerateRepaymentSchedule();
        var totalAmount = loan.RepaymentSchedule!.Installments.Sum(i => i.TotalAmount);

        // Act
        loan.ProcessRepayment(totalAmount);

        // Assert
        Assert.All(loan.RepaymentSchedule.Installments, i => Assert.Equal("Paid", i.Status));
        Assert.Equal("Repaid", loan.Status);
    }

    [Fact]
    public void ProcessRepayment_ShouldHandleMultipleInstallmentsPayment()
    {
        // Arrange
        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
        loan.GenerateRepaymentSchedule();
        
        var firstInstallment = loan.RepaymentSchedule!.Installments[0];
        var secondInstallment = loan.RepaymentSchedule!.Installments[1];
        
        var paymentAmount = firstInstallment.TotalAmount + secondInstallment.TotalAmount;

        // Act
        loan.ProcessRepayment(paymentAmount);

        // Assert
        Assert.Equal("Paid", loan.RepaymentSchedule.Installments[0].Status);
        Assert.Equal("Paid", loan.RepaymentSchedule.Installments[1].Status);
        Assert.Equal("Pending", loan.RepaymentSchedule.Installments[2].Status);
        Assert.Equal("Active", loan.Status);
    }

    [Fact]
    public void ProcessRepayment_ShouldHandleOverpayment_ByMarkingAllPaid()
    {
        // Arrange
        var loan = new Loan(Guid.NewGuid(), "user-1", 100m, "USD", "Approved", DateTime.UtcNow, 1, 0.05m);
        loan.GenerateRepaymentSchedule();
        var totalAmount = loan.RepaymentSchedule!.Installments.Sum(i => i.TotalAmount);
        var overpaymentAmount = totalAmount + 50m;

        // Act
        loan.ProcessRepayment(overpaymentAmount);

        // Assert
        Assert.All(loan.RepaymentSchedule.Installments, i => Assert.Equal("Paid", i.Status));
        Assert.Equal("Repaid", loan.Status);
        // Note: Current implementation swallows overpayment. 
        // In a real system, we might want to track this or refund it, but for now we ensure it doesn't break logic.
    }

    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Loan_WhenAmountIsZero_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 0m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m));
    }

    [Fact]
    public void Loan_WhenAmountIsNegative_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), "user-1", -500m, "USD", "Approved", DateTime.UtcNow, 12, 0.05m));
    }

    [Fact]
    public void Loan_WhenCurrencyIsEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "", "Approved", DateTime.UtcNow, 12, 0.05m));
        Assert.Throws<ArgumentException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "   ", "Approved", DateTime.UtcNow, 12, 0.05m));
    }

    [Fact]
    public void Loan_Constructor_NormalizesCurrencyToUpperCase()
    {
        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "usd", "Approved", DateTime.UtcNow, 12, 0.05m);
        Assert.Equal("USD", loan.Currency);
    }

    [Fact]
    public void Loan_WhenAnnualInterestRateIsZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 0m));
    }

    [Fact]
    public void Loan_WhenAnnualInterestRateIsNegative_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, -0.05m));
    }

    [Fact]
    public void Loan_WhenAnnualInterestRateExceedsOne_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 1.01m));
    }

    [Fact]
    public void Loan_WhenAnnualInterestRateIsStoredAsPercentageInteger_ThrowsArgumentOutOfRangeException()
    {
        // Catches the catastrophic bug where AnnualInterestRate=12 is passed instead of 0.12
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 12m));
    }

    [Fact]
    public void Loan_WhenAnnualInterestRateIsExactlyOne_DoesNotThrow()
    {
        var loan = new Loan(Guid.NewGuid(), "user-1", 1000m, "USD", "Approved", DateTime.UtcNow, 12, 1.0m);
        Assert.Equal(1.0m, loan.AnnualInterestRate);
    }

    // ── Installment domain methods ────────────────────────────────────────────

    [Fact]
    public void Installment_MarkAsPaid_SetsPaidAmountEqualToTotal()
    {
        var installment = new Installment(DateTime.UtcNow.AddMonths(1), 90m, 10m, 100m);

        installment.MarkAsPaid();

        Assert.Equal("Paid", installment.Status);
        Assert.Equal(100m, installment.PaidAmount);
    }

    [Fact]
    public void Installment_ApplyPayment_AccumulatesPaidAmountAndSetsPartiallyPaid()
    {
        var installment = new Installment(DateTime.UtcNow.AddMonths(1), 90m, 10m, 100m);

        installment.ApplyPayment(40m);

        Assert.Equal("PartiallyPaid", installment.Status);
        Assert.Equal(40m, installment.PaidAmount);
    }

    [Fact]
    public void Installment_Cancel_SetsCancelledStatus()
    {
        var installment = new Installment(DateTime.UtcNow.AddMonths(1), 90m, 10m, 100m);

        installment.Cancel();

        Assert.Equal("Cancelled", installment.Status);
    }
}
