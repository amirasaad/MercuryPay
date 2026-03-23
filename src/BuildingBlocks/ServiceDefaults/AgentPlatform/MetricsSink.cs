using Microsoft.Extensions.Logging;

namespace MercuryPay.BuildingBlocks.AgentPlatform;

public interface IAgentTaskMetricSink
{
    ValueTask RecordAsync(AgentTaskMetric metric, CancellationToken cancellationToken = default);
}

public sealed class InMemoryAgentTaskMetricSink : IAgentTaskMetricSink
{
    private readonly Lock _gate = new();
    private readonly List<AgentTaskMetric> _metrics = [];

    public IReadOnlyList<AgentTaskMetric> Snapshot()
    {
        lock (_gate)
        {
            return _metrics.ToArray();
        }
    }

    public ValueTask RecordAsync(AgentTaskMetric metric, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_gate)
        {
            _metrics.Add(metric);
        }

        return ValueTask.CompletedTask;
    }
}

public sealed class LoggerAgentTaskMetricSink(ILogger<LoggerAgentTaskMetricSink> logger) : IAgentTaskMetricSink
{
    private readonly ILogger<LoggerAgentTaskMetricSink> _logger = logger;

    public ValueTask RecordAsync(AgentTaskMetric metric, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _logger.LogInformation(
            "agentTask taskId={TaskId} agentType={AgentType} startTime={StartTime} endTime={EndTime} durationMs={DurationMs} success={Success} error={Error} inputEventId={InputEventId}",
            metric.TaskId,
            metric.AgentType,
            metric.StartTime,
            metric.EndTime,
            metric.Duration.TotalMilliseconds,
            metric.Success,
            metric.Error,
            metric.InputEventId
        );

        return ValueTask.CompletedTask;
    }
}
