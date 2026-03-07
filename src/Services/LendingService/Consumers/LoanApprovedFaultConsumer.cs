using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Metrics;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class LoanApprovedFaultConsumer(LendingDbContext context, ILogger<LoanApprovedFaultConsumer> logger) : IConsumer<Fault<LoanApproved>>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<LoanApprovedFaultConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<Fault<LoanApproved>> context)
    {
        var message = context.Message.Message;
        
        // Increment failure metric
        LendingServiceMetrics.DisbursementFailures.Add(1, new KeyValuePair<string, object?>("loan_id", message.LoanId));

        // Log errors from exceptions
        foreach (var exception in context.Message.Exceptions)
        {
            _logger.LogError("Loan disbursement failed for Loan {LoanId}. Exception: {Message}", message.LoanId, exception.Message);
        }

        var loan = await _context.Loans.FindAsync(message.LoanId);
        if (loan != null)
        {
            loan.MarkAsDisbursementFailed();
            await _context.SaveChangesAsync();
            _logger.LogInformation("Loan {LoanId} marked as DisbursementFailed", message.LoanId);
        }
        else
        {
             _logger.LogWarning("Loan {LoanId} not found when processing fault", message.LoanId);
        }
    }
}
