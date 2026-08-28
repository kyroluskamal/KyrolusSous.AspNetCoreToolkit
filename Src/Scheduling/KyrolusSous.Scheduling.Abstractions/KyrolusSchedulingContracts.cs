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

public interface IKyrolusJobScheduler
{
    void ScheduleCronJob<TJob>(string cronExpression, string? jobName = null, bool useDistributedLock = true) where TJob : class, IKyrolusJob;
    IReadOnlyList<KyrolusJobScheduleRegistration> GetRegisteredJobs();
}
