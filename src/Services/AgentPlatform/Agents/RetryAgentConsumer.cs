using MassTransit;
using MercuryPay.AgentPlatform.Infrastructure;
using MercuryPay.BuildingBlocks.AgentPlatform;
using MercuryPay.BuildingBlocks.Events;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.AgentPlatform.Agents;

public sealed class RetryAgentConsumer(
    ILogger<RetryAgentConsumer> logger,
    IPublishEndpoint publishEndpoint,
    IAgentTaskMetricSink metricSink,
    AgentPlatformDbContext dbContext
) : IConsumer<PaymentCaptureFailed>
{
    private readonly ILogger<RetryAgentConsumer> _logger = logger;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly IAgentTaskMetricSink _metricSink = metricSink;
    private readonly AgentPlatformDbContext _dbContext = dbContext;

    private static readonly RollingWindowCircuitBreaker CircuitBreaker = new(
        new CircuitBreakerOptions(
            FailureThreshold: 5,
            SamplingWindow: TimeSpan.FromMinutes(1),
            OpenDuration: TimeSpan.FromMinutes(2)
        )
    );

    private static readonly RetryBackoffOptions BackoffOptions = new(
        BaseDelay: TimeSpan.FromSeconds(2),
        MaxDelay: TimeSpan.FromMinutes(2),
        JitterFactor: 0
    );

    private const int MaxRetries = 3;

    public async Task Consume(ConsumeContext<PaymentCaptureFailed> context)
    {
        var start = DateTimeOffset.UtcNow;
        var eventId = (context.MessageId ?? NewDeterministicId(context.Message)).ToString();
        var success = false;
        string? error = null;
        var status = "Executed";

        try
        {
            if (!CircuitBreaker.TryAcquirePermission(start))
            {
                status = "RejectedByCircuitBreaker";
                return;
            }

            var reason = context.Message.Reason ?? string.Empty;
            if (reason.Contains("insufficient funds", StringComparison.OrdinalIgnoreCase))
            {
                await _publishEndpoint.Publish(
                    new PaymentRequiresReview(
                        context.Message.PaymentId,
                        context.Message.CaptureAttemptId,
                        $"Capture failed (non-retriable): {context.Message.Reason}",
                        DateTimeOffset.UtcNow
                    ),
                    context.CancellationToken
                );

                await _dbContext.SaveChangesAsync(context.CancellationToken);
                success = true;
                return;
            }

            var attempt = await _dbContext.RetryAttempts.FirstOrDefaultAsync(
                x => x.CaptureAttemptId == context.Message.CaptureAttemptId,
                context.CancellationToken
            );

            if (attempt is null)
            {
                attempt = new RetryAttemptState
                {
                    CaptureAttemptId = context.Message.CaptureAttemptId,
                    PaymentId = context.Message.PaymentId,
                    RetryCount = 0,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                _dbContext.RetryAttempts.Add(attempt);
            }

            attempt.RetryCount += 1;
            attempt.LastError = context.Message.Reason;
            attempt.UpdatedAt = DateTimeOffset.UtcNow;

            if (attempt.RetryCount > MaxRetries)
            {
                await _publishEndpoint.Publish(
                    new PaymentRequiresReview(
                        context.Message.PaymentId,
                        context.Message.CaptureAttemptId,
                        $"Max retries exceeded: {MaxRetries}. Last error: {context.Message.Reason}",
                        DateTimeOffset.UtcNow
                    ),
                    context.CancellationToken
                );

                await _dbContext.SaveChangesAsync(context.CancellationToken);
                success = true;
                return;
            }

            var delay = RetryBackoff.GetDelay(attempt.RetryCount, BackoffOptions);
            var scheduledAt = DateTimeOffset.UtcNow.Add(delay);

            await _publishEndpoint.Publish(
                new RetryPaymentCommand(
                    context.Message.PaymentId,
                    context.Message.CaptureAttemptId,
                    attempt.RetryCount,
                    scheduledAt,
                    context.Message.Reason ?? string.Empty
                ),
                context.CancellationToken
            );

            await _dbContext.SaveChangesAsync(context.CancellationToken);
            CircuitBreaker.RecordSuccess(DateTimeOffset.UtcNow);
            success = true;
        }
        catch (Exception ex)
        {
            CircuitBreaker.RecordFailure(DateTimeOffset.UtcNow);
            error = ex.Message;
            status = "Failed";
            throw;
        }
        finally
        {
            var end = DateTimeOffset.UtcNow;
            await _metricSink.RecordAsync(
                new AgentTaskMetric(
                    TaskId: Guid.NewGuid(),
                    AgentType: "RetryAgent",
                    StartTime: start,
                    EndTime: end,
                    Success: success,
                    InputEventId: eventId,
                    Error: error
                ),
                context.CancellationToken
            );

            _logger.LogInformation("RetryAgent processed eventId={EventId} status={Status}", eventId, status);
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
