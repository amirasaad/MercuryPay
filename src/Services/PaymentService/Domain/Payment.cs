using System.ComponentModel.DataAnnotations;

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

    public Payment(Guid id, string fromUserId, string toUserId, decimal amount, string currency, string status, Guid? referenceId = null)
    {
        Id = id;
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Amount = amount;
        Currency = currency;
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
