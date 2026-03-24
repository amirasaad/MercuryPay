using MassTransit;
using MercuryPay.BuildingBlocks.Events;

namespace MercuryPay.AgentPlatform.Agents;

public sealed class PaymentCreatedFaultConsumer(ILogger<PaymentCreatedFaultConsumer> logger, IPublishEndpoint publishEndpoint)
    : IConsumer<Fault<PaymentCreated>>
{
    private readonly ILogger<PaymentCreatedFaultConsumer> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task Consume(ConsumeContext<Fault<PaymentCreated>> context)
    {
        var message = context.Message.Message;
        var attemptId = GetCaptureAttemptId(context);
        var reason = string.Join("; ", context.Message.Exceptions.Select(x => x.Message));

        _logger.LogWarning(
            "Payment capture failed for PaymentId={PaymentId} CaptureAttemptId={CaptureAttemptId} Reason={Reason}",
            message.PaymentId,
            attemptId,
            reason
        );

        await _publishEndpoint.Publish(
            new PaymentCaptureFailed(
                message.PaymentId,
                attemptId,
                reason,
                DateTimeOffset.UtcNow
            ),
            context.CancellationToken
        );
    }

    private static Guid GetCaptureAttemptId(ConsumeContext<Fault<PaymentCreated>> context)
    {
        var faultedMessageId = context.Message.FaultedMessageId;
        if (faultedMessageId.HasValue)
        {
            return faultedMessageId.Value;
        }

        return context.MessageId ?? Guid.NewGuid();
    }
}

