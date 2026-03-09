namespace MercuryPay.BuildingBlocks.Events;

public record LoanRepaymentProcessed(Guid LoanId, string UserId, decimal Amount, bool Success, string FailureReason, DateTimeOffset Timestamp)
{
    // Required for MassTransit/Serialization
    protected LoanRepaymentProcessed() : this(default, default!, default, default, default!, default) { }
}
