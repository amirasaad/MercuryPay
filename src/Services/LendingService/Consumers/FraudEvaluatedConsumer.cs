using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.LendingService.Consumers;

public class FraudEvaluatedConsumer(LendingDbContext context, ILogger<FraudEvaluatedConsumer> logger, IPublishEndpoint publishEndpoint) : IConsumer<FraudEvaluated>
{
    private readonly LendingDbContext _context = context;
    private readonly ILogger<FraudEvaluatedConsumer> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

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
            _context.Update(loan);
            await _context.SaveChangesAsync();

            await _publishEndpoint.Publish(new LoanFraudDetected(
                loan.Id,
                loan.UserId,
                loan.Amount,
                loan.Currency,
                "Fraud detected",
                DateTimeOffset.UtcNow
            ));
        }
        else
        {
            _logger.LogInformation("Loan {LoanId} passed fraud check.", loan.Id);
            // Optional: If we had a 'PendingDisbursement' state, we could move to 'Disbursed' or 'Active' here.
            // But currently it goes directly to 'Approved' then 'Disbursed' via Payment.
        }
    }
}
