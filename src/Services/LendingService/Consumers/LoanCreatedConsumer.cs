using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class LoanCreatedConsumer(LendingDbContext context, ILogger<LoanCreatedConsumer> logger, IPublishEndpoint publishEndpoint) : IConsumer<LoanCreated>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LoanCreatedConsumer> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task Consume(ConsumeContext<LoanCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing loan approval for Loan {LoanId}", message.LoanId);

        var loan = await _context.Loans.FindAsync(message.LoanId);
        if (loan == null)
        {
            // Loan not yet visible — can happen if the consumer runs before the publishing
            // transaction commits (e.g., in-memory transport in tests). Throwing causes
            // MassTransit to retry so the consumer can find the loan once it is persisted.
            _logger.LogWarning("Loan {LoanId} not found; will retry", message.LoanId);
            throw new InvalidOperationException($"Loan {message.LoanId} not found, will retry.");
        }

        // Logic: For now, auto-approve everything
        loan.Approve();
        await _context.SaveChangesAsync();

        _logger.LogInformation("Loan {LoanId} approved", message.LoanId);

        // Publish LoanApproved event to trigger disbursement
        await _publishEndpoint.Publish(new LoanApproved(
            loan.Id,
            loan.UserId,
            loan.Amount,
            loan.Currency,
            DateTimeOffset.UtcNow
        ));
    }
}
