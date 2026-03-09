namespace MercuryPay.BuildingBlocks.Events;

public record FraudEvaluated(
    Guid PaymentId,
    bool IsApproved,
    int RiskScore,
    string Reason,
    DateTimeOffset Timestamp,
    Guid? ReferenceId = null
)
{
    // Required for MassTransit/Serialization
    protected FraudEvaluated() : this(default, default, default, default!, default, default) { }
}
