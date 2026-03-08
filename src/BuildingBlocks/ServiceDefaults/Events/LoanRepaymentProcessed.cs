namespace MercuryPay.BuildingBlocks.Events;

public record LoanRepaymentProcessed(Guid LoanId, string UserId, bool Success, string FailureReason, DateTimeOffset Timestamp);
