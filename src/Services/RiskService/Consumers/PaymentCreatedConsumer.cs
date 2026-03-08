using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.RiskService.Domain;
using MercuryPay.RiskService.Infrastructure;
using Microsoft.Extensions.Logging;

namespace MercuryPay.RiskService.Consumers;

public class PaymentCreatedConsumer(ILogger<PaymentCreatedConsumer> logger, RiskDbContext dbContext) : IConsumer<PaymentCreated>
{
    private void LogInformation(string message, params object[] args)
    {
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("{Message}", string.Format(message, args));
    }
    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        var message = context.Message;
        
       LogInformation("Evaluating risk for Payment {PaymentId}: Amount {Amount} {Currency}", message.PaymentId, message.Amount, message.Currency);

        // 1. Evaluate Risk
        var assessment = RiskAssessment.Evaluate(message.PaymentId, message.Amount, message.FromUserId);

        LogInformation("Risk Assessment for Payment {PaymentId}: Approved={Approved}, Score={Score}, Reason={Reason}", assessment.Id, assessment.IsApproved, assessment.RiskScore, assessment.Reason);

        // 2. Save assessment to database
        dbContext.RiskAssessments.Add(assessment);
        await dbContext.SaveChangesAsync();

        // 3. Publish FraudEvaluated Event
        LogInformation("Publishing FraudEvaluated Event for Payment {PaymentId}", message.PaymentId);
        await context.Publish(new FraudEvaluated(
            message.PaymentId,
            assessment.IsApproved,
            assessment.RiskScore,
            assessment.Reason,
            DateTimeOffset.UtcNow
        ));
    }
}
