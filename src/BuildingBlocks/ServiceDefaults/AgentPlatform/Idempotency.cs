using System.Collections.Concurrent;

namespace MercuryPay.BuildingBlocks.AgentPlatform;

public enum IdempotencyStatus
{
    InProgress = 0,
    Completed = 1
}

public readonly record struct IdempotencyEntry(IdempotencyStatus Status, DateTimeOffset ExpiresAt);

public interface IIdempotencyStore
{
    ValueTask<bool> TryBeginAsync(
        string key,
        DateTimeOffset now,
        TimeSpan lockTtl,
        CancellationToken cancellationToken = default
    );

    ValueTask MarkCompletedAsync(
        string key,
        DateTimeOffset now,
        TimeSpan completedTtl,
        CancellationToken cancellationToken = default
    );

    ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default);
}

public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly ConcurrentDictionary<string, IdempotencyEntry> _entries = new();

    public ValueTask<bool> TryBeginAsync(
        string key,
        DateTimeOffset now,
        TimeSpan lockTtl,
        CancellationToken cancellationToken = default
    )
    {
        if (lockTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lockTtl), "lockTtl must be > 0.");
        }

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_entries.TryGetValue(key, out var existing))
            {
                var proposed = new IdempotencyEntry(IdempotencyStatus.InProgress, now.Add(lockTtl));
                if (_entries.TryAdd(key, proposed))
                {
                    return ValueTask.FromResult(true);
                }

                continue;
            }

            if (existing.ExpiresAt > now)
            {
                return ValueTask.FromResult(false);
            }

            _entries.TryRemove(key, out _);
        }
    }

    public ValueTask MarkCompletedAsync(
        string key,
        DateTimeOffset now,
        TimeSpan completedTtl,
        CancellationToken cancellationToken = default
    )
    {
        if (completedTtl <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(completedTtl), "completedTtl must be > 0.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        _entries[key] = new IdempotencyEntry(IdempotencyStatus.Completed, now.Add(completedTtl));
        return ValueTask.CompletedTask;
    }

    public ValueTask ReleaseAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _entries.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}

