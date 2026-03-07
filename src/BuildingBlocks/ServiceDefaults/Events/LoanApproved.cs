namespace MercuryPay.BuildingBlocks.Events;

public record LoanApproved(
    Guid LoanId,
    string UserId,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp
)
{
    // Required for MassTransit/Serialization
    protected LoanApproved() : this(default, default!, default, default!, default) { }
}
