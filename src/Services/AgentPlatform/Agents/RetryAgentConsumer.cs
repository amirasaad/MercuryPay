using MassTransit;
using MercuryPay.BuildingBlocks.AgentPlatform;
using MercuryPay.BuildingBlocks.Events;

namespace MercuryPay.AgentPlatform.Agents;

public sealed class RetryAgentConsumer(
    ILogger<RetryAgentConsumer> logger,
    IPublishEndpoint publishEndpoint,
    IAgentTaskMetricSink metricSink
) : IConsumer<PaymentCaptureFailed>
{
    private readonly ILogger<RetryAgentConsumer> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAgentTaskMetricSink _metricSink = metricSink;

    private static readonly InMemoryIdempotencyStore IdempotencyStore = new();
    private static readonly RollingWindowCircuitBreaker CircuitBreaker = new(
        new CircuitBreakerOptions(
            FailureThreshold: 5,
            SamplingWindow: TimeSpan.FromMinutes(1),
            OpenDuration: TimeSpan.FromMinutes(2)
        )
    );
    private static readonly AgentEventProcessor Processor = new(
        agentType: "RetryAgent",
        idempotencyStore: IdempotencyStore,
        circuitBreaker: CircuitBreaker,
        options: new AgentEventProcessorOptions(
            LockTtl: TimeSpan.FromSeconds(30),
            CompletedTtl: TimeSpan.FromHours(24)
        )
    );

    private static readonly RetryBackoffOptions BackoffOptions = new(
        BaseDelay: TimeSpan.FromSeconds(2),
        MaxDelay: TimeSpan.FromMinutes(2),
        JitterFactor: 0
    );

    private const int MaxRetries = 3;
    private static readonly Dictionary<Guid, int> RetryCounts = new();
    private static readonly Lock RetryCountsGate = new();

    public async Task Consume(ConsumeContext<PaymentCaptureFailed> context)
    {
        var start = DateTimeOffset.UtcNow;
        var eventId = (context.MessageId ?? NewDeterministicId(context.Message)).ToString();

        var outcome = await Processor.ProcessAsync(
            eventId: eventId,
            now: start,
            handler: async ct =>
            {
                var retryAttempt = IncrementRetryCount(context.Message.CaptureAttemptId);

                if (retryAttempt > MaxRetries)
                {
                    await _publishEndpoint.Publish(
                        new PaymentRequiresReview(
                            context.Message.PaymentId,
                            context.Message.CaptureAttemptId,
                            $"Max retries exceeded: {MaxRetries}. Last error: {context.Message.Reason}",
                            DateTimeOffset.UtcNow
                        ),
                        ct
                    );
                    return;
                }

                var delay = RetryBackoff.GetDelay(retryAttempt, BackoffOptions);
                var scheduledAt = DateTimeOffset.UtcNow.Add(delay);

                await _publishEndpoint.Publish(
                    new RetryPaymentCommand(
                        context.Message.PaymentId,
                        context.Message.CaptureAttemptId,
                        retryAttempt,
                        scheduledAt,
                        context.Message.Reason
                    ),
                    ct
                );
            },
            cancellationToken: context.CancellationToken
        );

        var end = DateTimeOffset.UtcNow;
        await _metricSink.RecordAsync(
            new AgentTaskMetric(
                TaskId: Guid.NewGuid(),
                AgentType: "RetryAgent",
                StartTime: start,
                EndTime: end,
                Success: outcome.Status is AgentExecutionStatus.Executed or AgentExecutionStatus.DuplicateIgnored,
                InputEventId: eventId,
                Error: outcome.Error
            ),
            context.CancellationToken
        );

        _logger.LogInformation("RetryAgent processed eventId={EventId} status={Status}", eventId, outcome.Status);
    }

    private static int IncrementRetryCount(Guid captureAttemptId)
    {
        lock (RetryCountsGate)
        {
            if (!RetryCounts.TryGetValue(captureAttemptId, out var current))
            {
                RetryCounts[captureAttemptId] = 1;
                return 1;
            }

            var next = current + 1;
            RetryCounts[captureAttemptId] = next;
            return next;
        }
    }

    private static Guid NewDeterministicId(PaymentCaptureFailed message)
    {
        return GuidUtility.Create(
            GuidUtility.Namespaces.Url,
            $"PaymentCaptureFailed:{message.PaymentId}:{message.CaptureAttemptId}:{message.Timestamp:O}"
        );
    }
}

internal static class GuidUtility
{
    public static class Namespaces
    {
        public static readonly Guid Url = new("6ba7b811-9dad-11d1-80b4-00c04fd430c8");
    }

    public static Guid Create(Guid namespaceId, string name)
    {
        var namespaceBytes = namespaceId.ToByteArray();
        SwapByteOrder(namespaceBytes);

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(name);

        var hash = System.Security.Cryptography.SHA1.HashData([.. namespaceBytes, .. nameBytes]);

        var newGuid = hash[..16].ToArray();

        newGuid[6] = (byte)((newGuid[6] & 0x0F) | (5 << 4));
        newGuid[8] = (byte)((newGuid[8] & 0x3F) | 0x80);

        SwapByteOrder(newGuid);
        return new Guid(newGuid);
    }

    private static void SwapByteOrder(byte[] guid)
    {
        (guid[0], guid[3]) = (guid[3], guid[0]);
        (guid[1], guid[2]) = (guid[2], guid[1]);
        (guid[4], guid[5]) = (guid[5], guid[4]);
        (guid[6], guid[7]) = (guid[7], guid[6]);
    }
}
