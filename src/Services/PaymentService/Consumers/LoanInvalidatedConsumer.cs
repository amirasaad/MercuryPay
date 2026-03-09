using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.PaymentService.Consumers;

public class LoanInvalidatedConsumer(PaymentDbContext dbContext, ILogger<LoanInvalidatedConsumer> logger) : IConsumer<LoanInvalidated>
{
    private readonly PaymentDbContext _dbContext = dbContext;
    private readonly ILogger<LoanInvalidatedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanInvalidated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing LoanInvalidated event for Loan {LoanId}. Reason: {Reason}", message.LoanId, message.Reason);

        // Find pending payments for this loan
        var pendingPayments = await _dbContext.Payments
            .Where(p => p.ReferenceId == message.LoanId && p.Status == "Pending")
            .ToListAsync();

        if (pendingPayments.Count != 0)
        {
            foreach (var payment in pendingPayments)
            {
                _logger.LogWarning("Cancelling pending payment {PaymentId} for invalidated Loan {LoanId}", payment.Id, message.LoanId);
                payment.Reject($"Loan Invalidated: {message.Reason}");
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Cancelled {Count} pending payments for Loan {LoanId}", pendingPayments.Count, message.LoanId);
        }
        else
        {
            _logger.LogInformation("No pending payments found for invalidated Loan {LoanId}", message.LoanId);
        }
    }
}
