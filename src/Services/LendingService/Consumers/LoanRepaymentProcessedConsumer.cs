using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class LoanRepaymentProcessedConsumer(LendingDbContext context, ILogger<LoanRepaymentProcessedConsumer> logger) : IConsumer<LoanRepaymentProcessed>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LoanRepaymentProcessedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanRepaymentProcessed> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing repayment result for Loan {LoanId}, Success: {Success}", message.LoanId, message.Success);

        var loan = await _context.Loans.FindAsync(message.LoanId);
        if (loan == null)
        {
            _logger.LogWarning("Loan {LoanId} not found during repayment processing", message.LoanId);
            return;
        }

        if (message.Success)
        {
            loan.MarkAsRepaid();
            _logger.LogInformation("Loan {LoanId} marked as Repaid", message.LoanId);
        }
        else
        {
            loan.MarkAsRepaymentFailed();
            _logger.LogWarning("Loan {LoanId} repayment failed: {Reason}", message.LoanId, message.FailureReason);
        }

        await _context.SaveChangesAsync();
    }
}
