using RenTrek.BLL.Helpers;
using RenTrek.DAL.Repositories.Interfaces;

namespace RenTrek.BLL.Tests;

public sealed class AuthAttemptLimiterTests
{
    [Fact]
    public async Task LocksAfterMaxFailures_AndUnlocksAfterLockDuration()
    {
        var clock = new ManualTimeProvider(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
        var limiter = new AuthAttemptLimiter(new InMemoryAuthAttempts(), clock);

        for (var i = 1; i < AuthAttemptLimiter.MaxFailures; i++)
        {
            Assert.False(await limiter.RecordFailureAsync("login:a"));
        }

        Assert.False(await limiter.IsLockedAsync("login:a"));
        Assert.True(await limiter.RecordFailureAsync("login:a"));
        Assert.True(await limiter.IsLockedAsync("login:a"));
        Assert.False(await limiter.IsLockedAsync("login:b"));

        clock.Now += AuthAttemptLimiter.LockDuration;
        Assert.False(await limiter.IsLockedAsync("login:a"));
        Assert.False(await limiter.RecordFailureAsync("login:a"));
    }

    [Fact]
    public async Task Reset_ClearsFailures()
    {
        var limiter = new AuthAttemptLimiter(new InMemoryAuthAttempts(), TimeProvider.System);
        for (var i = 0; i < AuthAttemptLimiter.MaxFailures; i++)
        {
            await limiter.RecordFailureAsync("reset:a");
        }

        await limiter.ResetAsync("reset:a");

        Assert.False(await limiter.IsLockedAsync("reset:a"));
    }

    private sealed class ManualTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

/// <summary>Cùng ngữ nghĩa với upsert SQL trong AuthAttemptRepository.</summary>
internal sealed class InMemoryAuthAttempts : IAuthAttemptRepository
{
    private readonly Dictionary<string, (int Failures, DateTimeOffset WindowStart)> _rows = [];

    public Task<int> GetFailuresAsync(string key, DateTimeOffset windowCutoff, CancellationToken cancellationToken)
        => Task.FromResult(_rows.TryGetValue(key, out var r) && r.WindowStart > windowCutoff ? r.Failures : 0);

    public Task<int> RecordFailureAsync(string key, DateTimeOffset now, DateTimeOffset windowCutoff, CancellationToken cancellationToken)
    {
        var row = _rows.TryGetValue(key, out var r) && r.WindowStart > windowCutoff ? (r.Failures + 1, r.WindowStart) : (1, now);
        _rows[key] = row;
        return Task.FromResult(row.Item1);
    }

    public Task ResetAsync(string key, CancellationToken cancellationToken)
    {
        _rows.Remove(key);
        return Task.CompletedTask;
    }
}
