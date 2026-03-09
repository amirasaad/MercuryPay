namespace MercuryPay.BuildingBlocks.Events;

public record LoanInvalidated(
    Guid LoanId,
    string Reason,
    DateTimeOffset Timestamp
);
