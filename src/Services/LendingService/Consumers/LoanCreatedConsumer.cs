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

        // Simulate credit check delay
        await Task.Delay(2000);

        var loan = await _context.Loans.FindAsync(message.LoanId);
        if (loan == null)
        {
            _logger.LogWarning("Loan {LoanId} not found during processing", message.LoanId);
            return;
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
