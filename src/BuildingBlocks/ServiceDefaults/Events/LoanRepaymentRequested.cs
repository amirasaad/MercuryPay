namespace MercuryPay.BuildingBlocks.Events;

public record LoanRepaymentRequested(Guid LoanId, string UserId, decimal Amount, string Currency, DateTimeOffset Timestamp);
