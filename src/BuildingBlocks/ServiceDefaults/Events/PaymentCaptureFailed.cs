namespace MercuryPay.BuildingBlocks.Events;

public record PaymentCaptureFailed(
    Guid PaymentId,
    Guid CaptureAttemptId,
    string Reason,
    DateTimeOffset Timestamp
)
{
    protected PaymentCaptureFailed() : this(default, default, default!, default) { }
}

