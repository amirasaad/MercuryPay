using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.PaymentService.Consumers;

public sealed class RetryPaymentCommandConsumer(
    ILogger<RetryPaymentCommandConsumer> logger,
    PaymentDbContext dbContext
) : IConsumer<RetryPaymentCommand>
{
    private readonly ILogger<RetryPaymentCommandConsumer> _logger = logger;
    private readonly PaymentDbContext _dbContext = dbContext;

    public async Task Consume(ConsumeContext<RetryPaymentCommand> context)
    {
        var message = context.Message;
        var payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.Id == message.PaymentId, context.CancellationToken);
        if (payment is null)
        {
            _logger.LogWarning("RetryPaymentCommand received for missing PaymentId={PaymentId}", message.PaymentId);
            return;
        }

        if (!string.Equals(payment.Status, "Approved", StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "RetryPaymentCommand ignored for PaymentId={PaymentId} Status={Status}",
                payment.Id,
                payment.Status
            );
            return;
        }

        await context.Publish(
            new PaymentCreated(
                payment.Id,
                payment.FromUserId,
                payment.ToUserId,
                payment.Amount,
                payment.Currency,
                DateTimeOffset.UtcNow,
                payment.ReferenceId
            ),
            publishContext => publishContext.MessageId = message.CaptureAttemptId
        );

        await _dbContext.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "RetryPaymentCommand processed for PaymentId={PaymentId} CaptureAttemptId={CaptureAttemptId} Attempt={Attempt}",
            payment.Id,
            message.CaptureAttemptId,
            message.RetryAttempt
        );
    }
}

