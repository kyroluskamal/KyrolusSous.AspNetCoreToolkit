namespace KyrolusSous.Scheduling.Abstractions;

public sealed record KyrolusJobExecutionContext
{
    public required string JobName { get; init; }
    public DateTimeOffset ScheduledFireTimeUtc { get; init; }
    public DateTimeOffset ActualFireTimeUtc { get; init; }
    public CancellationToken CancellationToken { get; init; }
}

public interface IKyrolusJob
{
    Task ExecuteAsync(KyrolusJobExecutionContext context);
}

public interface IKyrolusJobLockProvider
{
    ValueTask<IAsyncDisposable?> TryAcquireLockAsync(string lockKey, TimeSpan lockDuration, CancellationToken cancellationToken = default);
}

public sealed record KyrolusJobScheduleRegistration
{
    public required string JobName { get; init; }
    public required Type JobType { get; init; }
    public required string CronExpression { get; init; }
    public bool UseDistributedLock { get; init; } = true;
    public TimeSpan LockDuration { get; init; } = TimeSpan.FromMinutes(5);
}

public sealed record KyrolusJobExecutionRecord
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public required string JobName { get; init; }
    public required DateTimeOffset StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public bool Succeeded { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan Duration => (CompletedAtUtc ?? DateTimeOffset.UtcNow) - StartedAtUtc;
}

public interface IKyrolusJobExecutionTracker
{
    Task RecordExecutionStartAsync(KyrolusJobExecutionRecord record, CancellationToken cancellationToken = default);
    Task RecordExecutionEndAsync(string recordId, bool succeeded, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KyrolusJobExecutionRecord>> GetRecentExecutionsAsync(int limit = 50, CancellationToken cancellationToken = default);
}

public sealed record KyrolusOneShotJobRegistration
{
    public required string JobName { get; init; }
    public required Type JobType { get; init; }
    public required DateTimeOffset FireAtUtc { get; init; }
    public object? State { get; init; }
}

public interface IKyrolusJobScheduler
{
    void ScheduleCronJob<TJob>(string cronExpression, string? jobName = null, bool useDistributedLock = true) where TJob : class, IKyrolusJob;
    void ScheduleOneShotJob<TJob>(DateTimeOffset fireAtUtc, string? jobName = null) where TJob : class, IKyrolusJob;
    IReadOnlyList<KyrolusJobScheduleRegistration> GetRegisteredJobs();
    IReadOnlyList<KyrolusOneShotJobRegistration> GetRegisteredOneShotJobs();
    Task<bool> TriggerJobNowAsync(string jobName, IServiceProvider serviceProvider, CancellationToken cancellationToken = default);
}
