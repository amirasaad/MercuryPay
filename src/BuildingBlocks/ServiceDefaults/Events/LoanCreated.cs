namespace MercuryPay.BuildingBlocks.Events;

public record LoanCreated(
    Guid LoanId,
    string UserId,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp
)
{
    // Required for MassTransit/Serialization
    protected LoanCreated() : this(default, default!, default, default!, default) { }
}
