namespace MercuryPay.RiskService.Domain;

public class RiskAssessment
{
    public Guid Id { get; private set; }
    public Guid PaymentId { get; private set; }
    public int RiskScore { get; private set; }
    public bool IsApproved { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private RiskAssessment() { }

    public static RiskAssessment Evaluate(Guid paymentId, decimal amount, string fromUserId)
    {
        var assessment = new RiskAssessment
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        // Simple Rule Engine
        if (amount > 10000)
        {
            assessment.RiskScore = 90;
            assessment.IsApproved = false;
            assessment.Reason = "High Value Transaction (> 10,000)";
        }
        else if (fromUserId.StartsWith("suspicious"))
        {
             assessment.RiskScore = 80;
             assessment.IsApproved = false;
             assessment.Reason = "Flagged User";
        }
        else
        {
            assessment.RiskScore = 10;
            assessment.IsApproved = true;
            assessment.Reason = "Low Risk";
        }

        return assessment;
    }
}
