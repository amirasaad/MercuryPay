namespace MercuryPay.RiskService.Domain;

/// <summary>
/// Risk Assessment Aggregate Root.
/// Represents the outcome of a fraud risk evaluation for a payment.
/// </summary>
public class RiskAssessment
{
    /// <summary>
    /// Unique identifier for this assessment.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// The payment being evaluated.
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Risk score from 0 (lowest) to 100 (highest).
    /// </summary>
    public int RiskScore { get; private set; }

    /// <summary>
    /// Whether this payment is approved or rejected.
    /// </summary>
    public bool IsApproved { get; private set; }

    /// <summary>
    /// Human-readable explanation for the decision.
    /// </summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>
    /// Timestamp when the assessment was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; private set; }

    // EF Core
    private RiskAssessment() { }

    /// <summary>
    /// Evaluate and create a risk assessment for a payment.
    /// Uses the enhanced rule engine for evaluation.
    /// </summary>
    public static RiskAssessment Evaluate(Guid paymentId, decimal amount, string fromUserId, string toUserId = "")
    {
        var engine = new RiskEngine();
        var evaluation = engine.Evaluate(amount, fromUserId, toUserId);

        var assessment = new RiskAssessment
        {
            Id = Guid.NewGuid(),
            PaymentId = paymentId,
            RiskScore = evaluation.RiskScore,
            IsApproved = evaluation.IsApproved,
            Reason = evaluation.Reason,
            CreatedAt = DateTimeOffset.UtcNow
        };

        return assessment;
    }
}
