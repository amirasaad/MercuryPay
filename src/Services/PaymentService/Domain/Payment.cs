namespace MercuryPay.PaymentService.Domain;

public class Payment
{
    public Guid Id { get; private set; }
    public string FromUserId { get; private set; }
    public string ToUserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public Guid? ReferenceId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string Status { get; private set; }
    public string? RejectionReason { get; private set; }

    public Payment(Guid id, string fromUserId, string toUserId, decimal amount, string currency, string status, Guid? referenceId = null)
    {
        if (amount <= 0)
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        if (string.IsNullOrWhiteSpace(fromUserId))
            throw new ArgumentException("FromUserId must not be empty.", nameof(fromUserId));
        if (string.IsNullOrWhiteSpace(toUserId))
            throw new ArgumentException("ToUserId must not be empty.", nameof(toUserId));
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency must not be empty.", nameof(currency));
        if (string.Equals(fromUserId.Trim(), toUserId.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Sender and receiver must be different users.", nameof(toUserId));

        Id = id;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
        Status = status;
        ReferenceId = referenceId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Approve()
    {
        Status = "Approved";
    }

    public void Reject(string reason)
    {
        Status = "Rejected";
        RejectionReason = reason;
    }

    // Required for EF Core
    private Payment() 
    {
        FromUserId = default!;
        ToUserId = default!;
        Currency = default!;
        Status = default!;
    }
}
