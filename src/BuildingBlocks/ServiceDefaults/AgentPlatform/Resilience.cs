
namespace MercuryPay.BuildingBlocks.AgentPlatform;

public sealed record CircuitBreakerOptions(
    int FailureThreshold,
    TimeSpan SamplingWindow,
    TimeSpan OpenDuration
);

public sealed class RollingWindowCircuitBreaker
{
    private readonly Lock _gate = new();
    private readonly Queue<DateTimeOffset> _failureTimestamps = new();
    private readonly CircuitBreakerOptions _options;

    private DateTimeOffset? _openUntil;
    private bool _halfOpenTrialUsed;

    public RollingWindowCircuitBreaker(CircuitBreakerOptions options)
    {
        if (options.FailureThreshold <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "FailureThreshold must be > 0.");
        }

        if (options.SamplingWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "SamplingWindow must be > 0.");
        }

        if (options.OpenDuration <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "OpenDuration must be > 0.");
        }

        _options = options;
    }

    public bool TryAcquirePermission(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_openUntil is null)
            {
                return true;
            }

            if (now < _openUntil.Value)
            {
                return false;
            }

            if (_halfOpenTrialUsed)
            {
                return false;
            }

            _halfOpenTrialUsed = true;
            return true;
        }
    }

    public void RecordSuccess(DateTimeOffset now)
    {
        lock (_gate)
        {
            if (_openUntil is not null && now >= _openUntil.Value)
            {
                _openUntil = null;
                _halfOpenTrialUsed = false;
                _failureTimestamps.Clear();
                return;
            }

            CleanupFailures(now);
        }
    }

    public void RecordFailure(DateTimeOffset now)
    {
        lock (_gate)
        {
            CleanupFailures(now);
            _failureTimestamps.Enqueue(now);

            if (_failureTimestamps.Count >= _options.FailureThreshold)
            {
                _openUntil = now.Add(_options.OpenDuration);
                _halfOpenTrialUsed = false;
            }
            else if (_openUntil is not null && now >= _openUntil.Value)
            {
                _openUntil = now.Add(_options.OpenDuration);
                _halfOpenTrialUsed = false;
            }
        }
    }

    public DateTimeOffset? GetOpenUntilForTests()
    {
        lock (_gate)
        {
            return _openUntil;
        }
    }

    private void CleanupFailures(DateTimeOffset now)
    {
        var cutoff = now.Subtract(_options.SamplingWindow);
        while (_failureTimestamps.Count > 0 && _failureTimestamps.Peek() <= cutoff)
        {
            _failureTimestamps.Dequeue();
        }
    }
}

public sealed record RetryBackoffOptions(
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    double JitterFactor
);

public static class RetryBackoff
{
    public static TimeSpan GetDelay(int attempt, RetryBackoffOptions options, double unitRandom = 0.5)
    {
        if (attempt <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt), "attempt must be >= 1.");
        }

        if (options.BaseDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "BaseDelay must be > 0.");
        }

        if (options.MaxDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDelay must be > 0.");
        }

        if (options.MaxDelay < options.BaseDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MaxDelay must be >= BaseDelay.");
        }

        if (options.JitterFactor < 0 || options.JitterFactor > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "JitterFactor must be in [0, 1].");
        }

        if (unitRandom < 0 || unitRandom > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(unitRandom), "unitRandom must be in [0, 1].");
        }

        var exponent = attempt - 1;
        var multiplier = Math.Pow(2, exponent);
        var rawMs = options.BaseDelay.TotalMilliseconds * multiplier;
        var cappedMs = Math.Min(options.MaxDelay.TotalMilliseconds, rawMs);

        if (options.JitterFactor == 0)
        {
            return TimeSpan.FromMilliseconds(cappedMs);
        }

        var r = (unitRandom * 2) - 1;
        var jitteredMs = cappedMs * (1 + (options.JitterFactor * r));
        if (jitteredMs < 0)
        {
            jitteredMs = 0;
        }

        return TimeSpan.FromMilliseconds(jitteredMs);
    }
}

