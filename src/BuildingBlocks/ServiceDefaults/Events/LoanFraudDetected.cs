namespace MercuryPay.BuildingBlocks.Events;

public record LoanFraudDetected(
    Guid LoanId,
    string UserId,
    decimal Amount,
    string Currency,
    string Reason,
    DateTimeOffset Timestamp
);
