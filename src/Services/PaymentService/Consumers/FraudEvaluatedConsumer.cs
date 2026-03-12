using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.PaymentService.Consumers;

public class FraudEvaluatedConsumer(ILogger<FraudEvaluatedConsumer> logger, PaymentDbContext dbContext) : IConsumer<FraudEvaluated>
{
    private readonly ILogger<FraudEvaluatedConsumer> _logger = logger;
    private readonly PaymentDbContext _dbContext = dbContext;

    public async Task Consume(ConsumeContext<FraudEvaluated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing Fraud Evaluation for Payment {PaymentId}: Approved={Approved}, Score={Score}",
            message.PaymentId, message.IsApproved, message.RiskScore);

        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == message.PaymentId);
        
        if (payment == null)
        {
            _logger.LogWarning("Payment {PaymentId} not found during fraud evaluation processing", message.PaymentId);
            return;
        }

        // Only update status if payment is still pending; do not override terminal states
        if (payment.Status != "Pending")
        {
            _logger.LogInformation("Skipping fraud status update for Payment {PaymentId} with current Status {Status}", payment.Id, payment.Status);
            return;
        }

        if (message.IsApproved)
        {
            payment.Approve();
            _logger.LogInformation("Payment {PaymentId} Approved by Risk Service", message.PaymentId);
        }
        else
        {
            payment.Reject(message.Reason);
            _logger.LogWarning("Payment {PaymentId} Rejected by Risk Service: {Reason}", message.PaymentId, message.Reason);
        }

        await _dbContext.SaveChangesAsync();
    }
}
