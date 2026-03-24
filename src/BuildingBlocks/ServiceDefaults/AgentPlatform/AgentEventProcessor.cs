namespace MercuryPay.BuildingBlocks.AgentPlatform;

public sealed record AgentEventProcessorOptions(TimeSpan LockTtl, TimeSpan CompletedTtl);

public enum AgentExecutionStatus
{
    Executed = 0,
    DuplicateIgnored = 1,
    RejectedByCircuitBreaker = 2,
    Failed = 3
}

public sealed record AgentExecutionOutcome(
    AgentExecutionStatus Status,
    string AgentType,
    string EventId,
    string? Error
);

public sealed class AgentEventProcessor
{
    private readonly string _agentType;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly RollingWindowCircuitBreaker _circuitBreaker;
    private readonly AgentEventProcessorOptions _options;

    public AgentEventProcessor(
        string agentType,
        IIdempotencyStore idempotencyStore,
        RollingWindowCircuitBreaker circuitBreaker,
        AgentEventProcessorOptions options
    )
    {
        if (string.IsNullOrWhiteSpace(agentType))
        {
            throw new ArgumentException("agentType is required.", nameof(agentType));
        }

        if (options.LockTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "LockTtl must be > 0.");
        }

        if (options.CompletedTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "CompletedTtl must be > 0.");
        }

        _agentType = agentType;
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
        _options = options;
    }

    public async Task<AgentExecutionOutcome> ProcessAsync(
        string eventId,
        DateTimeOffset now,
        Func<CancellationToken, Task> handler,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException("eventId is required.", nameof(eventId));
        }

        ArgumentNullException.ThrowIfNull(handler);

        if (!_circuitBreaker.TryAcquirePermission(now))
        {
            return new AgentExecutionOutcome(AgentExecutionStatus.RejectedByCircuitBreaker, _agentType, eventId, null);
        }

        var key = $"{_agentType}:{eventId}";
        var began = await _idempotencyStore.TryBeginAsync(key, now, _options.LockTtl, cancellationToken);
        if (!began)
        {
            return new AgentExecutionOutcome(AgentExecutionStatus.DuplicateIgnored, _agentType, eventId, null);
        }

        try
        {
            await handler(cancellationToken);
            await _idempotencyStore.MarkCompletedAsync(key, now, _options.CompletedTtl, cancellationToken);
            _circuitBreaker.RecordSuccess(now);
            return new AgentExecutionOutcome(AgentExecutionStatus.Executed, _agentType, eventId, null);
        }
        catch (Exception ex)
        {
            await _idempotencyStore.ReleaseAsync(key, cancellationToken);
            _circuitBreaker.RecordFailure(now);
            return new AgentExecutionOutcome(AgentExecutionStatus.Failed, _agentType, eventId, ex.Message);
        }
    }
}

