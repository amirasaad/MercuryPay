namespace MercuryPay.LendingService.Domain;

public class Loan(Guid id, string userId, decimal amount, string currency, string status, DateTime createdAt)
{
    public Guid Id { get; private set; } = id;
    public string UserId { get; private set; } = userId;
    public decimal Amount { get; private set; } = amount;
    public string Currency { get; private set; } = currency;
    public string Status { get; private set; } = status;
    public DateTime CreatedAt { get; private set; } = createdAt;

    public void MarkAsDisbursementFailed()
    {
        Status = "DisbursementFailed";
    }

    public void RetryDisbursement()
    {
        Status = "Approved";
    }

    public void Approve()
    {
        Status = "Approved";
    }

    public void MarkAsRepaymentProcessing()
    {
        Status = "RepaymentProcessing";
    }

    public void MarkAsRepaid()
    {
        Status = "Repaid";
    }

    public void MarkAsRepaymentFailed()
    {
        Status = "RepaymentFailed";
    }
}
