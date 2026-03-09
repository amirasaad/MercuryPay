namespace MercuryPay.BuildingBlocks.Events;

public record LoanRepaymentRequested(Guid LoanId, string UserId, decimal Amount, string Currency, DateTimeOffset Timestamp)
{
    // Required for MassTransit/Serialization
    protected LoanRepaymentRequested() : this(default, default!, default, default!, default) { }
}
