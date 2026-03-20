namespace MercuryPay.BuildingBlocks.AgentPlatform;

public sealed record AgentTaskMetric(
    Guid TaskId,
    string AgentType,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool Success,
    string InputEventId,
    string? Error = null
)
{
    public TimeSpan Duration => EndTime - StartTime;
}

public readonly record struct SuccessRateResult(int Total, int Successes, double SuccessRate);

public static class AgentEvaluation
{
    public static SuccessRateResult CalculateSuccessRate(
        IEnumerable<AgentTaskMetric> tasks,
        string agentType,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd
    )
    {
        if (windowEnd < windowStart)
        {
            throw new ArgumentOutOfRangeException(nameof(windowEnd), "windowEnd must be >= windowStart.");
        }

        var total = 0;
        var successes = 0;

        foreach (var task in tasks)
        {
            if (!string.Equals(task.AgentType, agentType, StringComparison.Ordinal))
            {
                continue;
            }

            if (task.StartTime < windowStart || task.StartTime >= windowEnd)
            {
                continue;
            }

            total++;
            if (task.Success)
            {
                successes++;
            }
        }

        var successRate = total == 0 ? 1.0 : (double)successes / total;
        return new SuccessRateResult(total, successes, successRate);
    }

    public static bool IsAnomalous(
        SuccessRateResult result,
        double minSuccessRate,
        int minSamples
    )
    {
        if (minSuccessRate < 0 || minSuccessRate > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(minSuccessRate), "minSuccessRate must be in [0, 1].");
        }

        if (minSamples < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minSamples), "minSamples must be >= 0.");
        }

        if (result.Total < minSamples)
        {
            return false;
        }

        return result.SuccessRate < minSuccessRate;
    }
}

