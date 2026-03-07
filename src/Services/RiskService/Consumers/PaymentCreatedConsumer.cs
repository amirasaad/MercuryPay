using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.RiskService.Domain;

namespace MercuryPay.RiskService.Consumers;

public class PaymentCreatedConsumer : IConsumer<PaymentCreated>
{
    private readonly ILogger<PaymentCreatedConsumer> _logger;
    // In a real app, we would inject a Repository here to save the assessment
    // private readonly IRiskRepository _repository;

    public PaymentCreatedConsumer(ILogger<PaymentCreatedConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Evaluating risk for Payment {PaymentId}: Amount {Amount} {Currency}", 
            message.PaymentId, message.Amount, message.Currency);

        // 1. Evaluate Risk
        var assessment = RiskAssessment.Evaluate(message.PaymentId, message.Amount, message.FromUserId);

        _logger.LogInformation("Risk Assessment for Payment {PaymentId}: Approved={Approved}, Score={Score}, Reason={Reason}",
            message.PaymentId, assessment.IsApproved, assessment.RiskScore, assessment.Reason);

        // 2. Publish FraudEvaluated Event
        await context.Publish(new FraudEvaluated(
            message.PaymentId,
            assessment.IsApproved,
            assessment.RiskScore,
            assessment.Reason,
            DateTimeOffset.UtcNow
        ));
        
        // TODO: Save assessment to database
    }
}
