using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class FraudEvaluatedConsumer(LendingDbContext context, ILogger<FraudEvaluatedConsumer> logger) : IConsumer<FraudEvaluated>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<FraudEvaluatedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<FraudEvaluated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing FraudEvaluated for Payment {PaymentId}, ReferenceId {ReferenceId}, Approved: {Approved}", 
            message.PaymentId, message.ReferenceId, message.IsApproved);

        if (message.ReferenceId == null)
        {
            _logger.LogDebug("FraudEvaluated event has no ReferenceId, ignoring in LendingService.");
            return;
        }

        var loan = await _context.Loans.FindAsync(message.ReferenceId);
        if (loan == null)
        {
            _logger.LogWarning("Loan with ID {LoanId} (from ReferenceId) not found.", message.ReferenceId);
            return;
        }

        if (!message.IsApproved)
        {
            _logger.LogWarning("Loan {LoanId} flagged as fraud. Marking as FraudDetected.", loan.Id);
            loan.MarkAsFraudDetected();
            await _context.SaveChangesAsync();
        }
        else
        {
            _logger.LogInformation("Loan {LoanId} passed fraud check.", loan.Id);
            // Optional: If we had a 'PendingDisbursement' state, we could move to 'Disbursed' or 'Active' here.
            // But currently it goes directly to 'Approved' then 'Disbursed' via Payment.
        }
    }
}
