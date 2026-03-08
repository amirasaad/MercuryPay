namespace MercuryPay.BuildingBlocks.Events;

public record LoanRepaymentProcessed(Guid LoanId, string UserId, decimal Amount, bool Success, string FailureReason, DateTimeOffset Timestamp);
