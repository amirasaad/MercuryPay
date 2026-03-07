namespace MercuryPay.BuildingBlocks.Events;

public record PaymentCreated(
    Guid PaymentId,
    string FromUserId,
    string ToUserId,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp
);
