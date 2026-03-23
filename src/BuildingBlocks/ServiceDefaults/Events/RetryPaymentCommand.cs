namespace MercuryPay.BuildingBlocks.Events;

public record RetryPaymentCommand(
    Guid PaymentId,
    Guid CaptureAttemptId,
    int RetryAttempt,
    DateTimeOffset ScheduledAt,
    string LastError
)
{
    protected RetryPaymentCommand() : this(default, default, default, default, default!) { }
}

