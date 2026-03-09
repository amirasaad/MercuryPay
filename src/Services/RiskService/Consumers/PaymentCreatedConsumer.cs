using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.RiskService.Domain;
using MercuryPay.RiskService.Infrastructure;
using Microsoft.Extensions.Logging;

namespace MercuryPay.RiskService.Consumers;

public partial class PaymentCreatedConsumer(ILogger<PaymentCreatedConsumer> logger, RiskDbContext dbContext) : IConsumer<PaymentCreated>
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Evaluating risk for Payment {PaymentId}: Amount {Amount} {Currency}")]
    private static partial void LogEvaluatingRisk(ILogger logger, Guid paymentId, decimal amount, string currency);

    [LoggerMessage(Level = LogLevel.Information, Message = "Risk Assessment for Payment {PaymentId}: Approved={Approved}, Score={Score}, Reason={Reason}")]
    private static partial void LogRiskAssessment(ILogger logger, Guid paymentId, bool approved, int score, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Publishing FraudEvaluated Event for Payment {PaymentId}")]
    private static partial void LogPublishingFraudEvaluated(ILogger logger, Guid paymentId);

    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        var message = context.Message;
        
        LogEvaluatingRisk(logger, message.PaymentId, message.Amount, message.Currency);

        // 1. Evaluate Risk (using the new Rule Engine)
        var assessment = RiskAssessment.Evaluate(message.PaymentId, message.Amount, message.FromUserId, message.ToUserId);

        LogRiskAssessment(logger, message.PaymentId, assessment.IsApproved, assessment.RiskScore, assessment.Reason);

        // 2. Save assessment to database
        dbContext.RiskAssessments.Add(assessment);
        // await dbContext.SaveChangesAsync(); // Removed to ensure atomicity with Outbox

        // 3. Publish FraudEvaluated Event
        LogPublishingFraudEvaluated(logger, message.PaymentId);
        await context.Publish(new FraudEvaluated(
            message.PaymentId,
            assessment.IsApproved,
            assessment.RiskScore,
            assessment.Reason,
            DateTimeOffset.UtcNow,
            message.ReferenceId
        ));
        
        // Ensure EF Outbox dispatches the published message within the same unit of work
        // This saves both the entity and the outbox message in a single transaction
        await dbContext.SaveChangesAsync();
    }
}
