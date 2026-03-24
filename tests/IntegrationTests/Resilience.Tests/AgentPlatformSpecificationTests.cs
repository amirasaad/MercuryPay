using MercuryPay.BuildingBlocks.AgentPlatform;
using Xunit;

namespace Resilience.Tests;

public class IdempotencyContractTests
{
    [Fact]
    public async Task InMemoryIdempotencyStore_AllowsFirstBegin_RejectsSecond_ThenAllowsAfterExpiry()
    {
        var store = new InMemoryIdempotencyStore();
        var key = "RetryAgent:evt-1";
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = await store.TryBeginAsync(key, now, TimeSpan.FromSeconds(10));
        Assert.True(first);

        await store.MarkCompletedAsync(key, now, TimeSpan.FromMinutes(1));

        var second = await store.TryBeginAsync(key, now.AddSeconds(1), TimeSpan.FromSeconds(10));
        Assert.False(second);

        var third = await store.TryBeginAsync(key, now.AddMinutes(2), TimeSpan.FromSeconds(10));
        Assert.True(third);
    }

    [Fact]
    public async Task InMemoryIdempotencyStore_AllowsRetry_WhenProcessingFailedAndReleased()
    {
        var store = new InMemoryIdempotencyStore();
        var key = "RetryAgent:evt-2";
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var first = await store.TryBeginAsync(key, now, TimeSpan.FromSeconds(10));
        Assert.True(first);

        await store.ReleaseAsync(key);

        var second = await store.TryBeginAsync(key, now.AddSeconds(1), TimeSpan.FromSeconds(10));
        Assert.True(second);
    }
}

public class RetryBackoffContractTests
{
    [Fact]
    public void RetryBackoff_ComputesExponentialDelay_AndCapsAtMaxDelay()
    {
        var options = new RetryBackoffOptions(
            BaseDelay: TimeSpan.FromSeconds(1),
            MaxDelay: TimeSpan.FromSeconds(10),
            JitterFactor: 0
        );

        Assert.Equal(TimeSpan.FromSeconds(1), RetryBackoff.GetDelay(1, options));
        Assert.Equal(TimeSpan.FromSeconds(2), RetryBackoff.GetDelay(2, options));
        Assert.Equal(TimeSpan.FromSeconds(8), RetryBackoff.GetDelay(4, options));
        Assert.Equal(TimeSpan.FromSeconds(10), RetryBackoff.GetDelay(5, options));
        Assert.Equal(TimeSpan.FromSeconds(10), RetryBackoff.GetDelay(10, options));
    }

    [Fact]
    public void RetryBackoff_AppliesBoundedJitter()
    {
        var options = new RetryBackoffOptions(
            BaseDelay: TimeSpan.FromSeconds(10),
            MaxDelay: TimeSpan.FromSeconds(10),
            JitterFactor: 0.2
        );

        var min = RetryBackoff.GetDelay(1, options, unitRandom: 0);
        var max = RetryBackoff.GetDelay(1, options, unitRandom: 1);

        Assert.Equal(TimeSpan.FromSeconds(8), min);
        Assert.Equal(TimeSpan.FromSeconds(12), max);
    }
}

public class CircuitBreakerContractTests
{
    [Fact]
    public void CircuitBreaker_OpensAfterThreshold_AndRejectsUntilOpenDurationElapses()
    {
        var options = new CircuitBreakerOptions(
            FailureThreshold: 3,
            SamplingWindow: TimeSpan.FromMinutes(1),
            OpenDuration: TimeSpan.FromMinutes(2)
        );
        var breaker = new RollingWindowCircuitBreaker(options);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        breaker.RecordFailure(t0);
        breaker.RecordFailure(t0.AddSeconds(10));
        breaker.RecordFailure(t0.AddSeconds(20));

        Assert.False(breaker.TryAcquirePermission(t0.AddSeconds(30)));

        Assert.True(breaker.TryAcquirePermission(t0.AddMinutes(2).AddSeconds(20)));
        Assert.False(breaker.TryAcquirePermission(t0.AddMinutes(2).AddSeconds(21)));
    }

    [Fact]
    public void CircuitBreaker_ClosesOnSuccessfulTrial_AndReopensOnFailedTrial()
    {
        var options = new CircuitBreakerOptions(
            FailureThreshold: 2,
            SamplingWindow: TimeSpan.FromMinutes(1),
            OpenDuration: TimeSpan.FromMinutes(1)
        );
        var breaker = new RollingWindowCircuitBreaker(options);
        var t0 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        breaker.RecordFailure(t0);
        breaker.RecordFailure(t0.AddSeconds(1));

        var trialTime = t0.AddMinutes(1).AddSeconds(1);
        Assert.True(breaker.TryAcquirePermission(trialTime));
        breaker.RecordSuccess(trialTime);
        Assert.True(breaker.TryAcquirePermission(trialTime.AddSeconds(1)));

        breaker.RecordFailure(trialTime.AddSeconds(2));
        breaker.RecordFailure(trialTime.AddSeconds(3));

        var failedTrialTime = trialTime.AddMinutes(1).AddSeconds(4);
        Assert.True(breaker.TryAcquirePermission(failedTrialTime));
        breaker.RecordFailure(failedTrialTime);
        Assert.False(breaker.TryAcquirePermission(failedTrialTime.AddSeconds(1)));
    }
}

public class AgentEventProcessorContractTests
{
    [Fact]
    public async Task AgentEventProcessor_ExecutesHandlerOnce_ForDuplicateDeliveries()
    {
        var store = new InMemoryIdempotencyStore();
        var breaker = new RollingWindowCircuitBreaker(new CircuitBreakerOptions(10, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));
        var processor = new AgentEventProcessor(
            agentType: "RetryAgent",
            idempotencyStore: store,
            circuitBreaker: breaker,
            options: new AgentEventProcessorOptions(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1))
        );

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var executed = 0;

        Task Handler(CancellationToken _) 
        { 
            executed++; 
            return Task.CompletedTask; 
        }

        var first = await processor.ProcessAsync("evt-1", now, Handler);
        var second = await processor.ProcessAsync("evt-1", now.AddSeconds(1), Handler);

        Assert.Equal(AgentExecutionStatus.Executed, first.Status);
        Assert.Equal(AgentExecutionStatus.DuplicateIgnored, second.Status);
        Assert.Equal(1, executed);
    }

    [Fact]
    public async Task AgentEventProcessor_AllowsRetry_WhenHandlerFails()
    {
        var store = new InMemoryIdempotencyStore();
        var breaker = new RollingWindowCircuitBreaker(new CircuitBreakerOptions(10, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1)));
        var processor = new AgentEventProcessor(
            agentType: "RetryAgent",
            idempotencyStore: store,
            circuitBreaker: breaker,
            options: new AgentEventProcessorOptions(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1))
        );

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var attempts = 0;

        Task Handler(CancellationToken _)
        {
            attempts++;
            if (attempts == 1)
            {
                throw new InvalidOperationException("boom");
            }
            return Task.CompletedTask;
        }

        var first = await processor.ProcessAsync("evt-2", now, Handler);
        var second = await processor.ProcessAsync("evt-2", now.AddSeconds(1), Handler);

        Assert.Equal(AgentExecutionStatus.Failed, first.Status);
        Assert.Equal(AgentExecutionStatus.Executed, second.Status);
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task AgentEventProcessor_RejectsExecution_WhenCircuitBreakerIsOpen()
    {
        var store = new InMemoryIdempotencyStore();
        var breaker = new RollingWindowCircuitBreaker(new CircuitBreakerOptions(1, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5)));
        var processor = new AgentEventProcessor(
            agentType: "RetryAgent",
            idempotencyStore: store,
            circuitBreaker: breaker,
            options: new AgentEventProcessorOptions(TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(1))
        );

        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        breaker.RecordFailure(now);

        var outcome = await processor.ProcessAsync("evt-3", now.AddSeconds(1), _ => Task.CompletedTask);
        Assert.Equal(AgentExecutionStatus.RejectedByCircuitBreaker, outcome.Status);
    }
}

public class EvaluationContractTests
{
    [Fact]
    public void AgentEvaluation_DetectsAnomaly_WhenSuccessRateBelowThreshold_AndMinSamplesMet()
    {
        var agentType = "RetryAgent";
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddMinutes(5);

        var tasks = new List<AgentTaskMetric>();
        for (var i = 0; i < 94; i++)
        {
            tasks.Add(new AgentTaskMetric(Guid.NewGuid(), agentType, windowStart.AddSeconds(i), windowStart.AddSeconds(i + 1), true, $"evt-{i}"));
        }
        for (var i = 94; i < 100; i++)
        {
            tasks.Add(new AgentTaskMetric(Guid.NewGuid(), agentType, windowStart.AddSeconds(i), windowStart.AddSeconds(i + 1), false, $"evt-{i}", "failed"));
        }

        var result = AgentEvaluation.CalculateSuccessRate(tasks, agentType, windowStart, windowEnd);
        var anomalous = AgentEvaluation.IsAnomalous(result, minSuccessRate: 0.95, minSamples: 100);

        Assert.Equal(100, result.Total);
        Assert.Equal(94, result.Successes);
        Assert.True(anomalous);
    }

    [Fact]
    public void AgentEvaluation_DoesNotDetectAnomaly_WhenMinSamplesNotMet()
    {
        var agentType = "RetryAgent";
        var windowStart = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var windowEnd = windowStart.AddMinutes(5);

        var tasks = new List<AgentTaskMetric>
        {
            new(Guid.NewGuid(), agentType, windowStart.AddSeconds(1), windowStart.AddSeconds(2), false, "evt-1", "failed")
        };

        var result = AgentEvaluation.CalculateSuccessRate(tasks, agentType, windowStart, windowEnd);
        var anomalous = AgentEvaluation.IsAnomalous(result, minSuccessRate: 0.95, minSamples: 100);

        Assert.False(anomalous);
    }
}
