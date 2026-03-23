namespace MercuryPay.BuildingBlocks.Events;

public record PaymentRequiresReview(
    Guid PaymentId,
    Guid CaptureAttemptId,
    string Reason,
    DateTimeOffset Timestamp
)
{
    protected PaymentRequiresReview() : this(default, default, default!, default) { }
}

