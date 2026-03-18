using MercuryPay.PaymentService.Domain;
using Xunit;

namespace MercuryPay.PaymentService.Tests;

public class PaymentDomainTests
{
    // ── Constructor guards ────────────────────────────────────────────────────

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenAmountIsZero()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "user_b", 0m, "USD", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenAmountIsNegative()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "user_b", -100m, "USD", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenFromUserIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "", "user_b", 100m, "USD", "Pending"));
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "   ", "user_b", 100m, "USD", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenToUserIdIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "", 100m, "USD", "Pending"));
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "   ", 100m, "USD", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenCurrencyIsEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "user_b", 100m, "", "Pending"));
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "user_b", 100m, "   ", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_ThrowsArgumentException_WhenSenderAndReceiverAreTheSame()
    {
        Assert.Throws<ArgumentException>(() =>
            new Payment(Guid.NewGuid(), "user_a", "user_a", 100m, "USD", "Pending"));
    }

    [Fact]
    public void Payment_Constructor_NormalizesCurrencyToUpperCase()
    {
        var payment = new Payment(Guid.NewGuid(), "user_a", "user_b", 100m, "usd", "Pending");
        Assert.Equal("USD", payment.Currency);
    }

    // ── Reject stores reason ─────────────────────────────────────────────────

    [Fact]
    public void Reject_StoresRejectionReason_AndSetsStatusToRejected()
    {
        var payment = new Payment(Guid.NewGuid(), "user_a", "user_b", 100m, "USD", "Pending");

        payment.Reject("Fraud detected: high-risk user");

        Assert.Equal("Rejected", payment.Status);
        Assert.Equal("Fraud detected: high-risk user", payment.RejectionReason);
    }

    [Fact]
    public void Reject_WithEmptyReason_SetsStatusToRejected()
    {
        var payment = new Payment(Guid.NewGuid(), "user_a", "user_b", 100m, "USD", "Pending");

        payment.Reject(string.Empty);

        Assert.Equal("Rejected", payment.Status);
        Assert.Equal(string.Empty, payment.RejectionReason);
    }

    [Fact]
    public void Approve_SetsStatusToApproved_AndLeavesRejectionReasonNull()
    {
        var payment = new Payment(Guid.NewGuid(), "user_a", "user_b", 50m, "EUR", "Pending");

        payment.Approve();

        Assert.Equal("Approved", payment.Status);
        Assert.Null(payment.RejectionReason);
    }
}
