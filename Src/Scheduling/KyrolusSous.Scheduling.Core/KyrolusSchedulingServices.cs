using System.Collections.Concurrent;
using KyrolusSous.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.Scheduling.Core;

public static class KyrolusCronParser
{
    public static DateTimeOffset? GetNextOccurrence(string cronExpression, DateTimeOffset baseTimeUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cronExpression);

        var parts = cronExpression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 5)
        {
            throw new FormatException($"Invalid cron expression '{cronExpression}'. Expected at least 5 fields: minute hour day-of-month month day-of-week.");
        }

        // Standard 5-field cron: min hour dom month dow
        var minutePart = parts[0];

        if (minutePart.StartsWith("*/"))
        {
            if (int.TryParse(minutePart[2..], out var interval) && interval > 0)
            {
                var nextMin = ((baseTimeUtc.Minute / interval) + 1) * interval;
                var addedMinutes = nextMin - baseTimeUtc.Minute;
                return baseTimeUtc.AddMinutes(addedMinutes).AddSeconds(-baseTimeUtc.Second);
            }
        }
        else if (minutePart == "*")
        {
            return baseTimeUtc.AddMinutes(1).AddSeconds(-baseTimeUtc.Second);
        }
        else if (int.TryParse(minutePart, out var targetMin))
        {
            var next = baseTimeUtc.AddMinutes(1).AddSeconds(-baseTimeUtc.Second);
            while (next.Minute != targetMin)
            {
                next = next.AddMinutes(1);
            }
            return next;
        }

        return baseTimeUtc.AddMinutes(1);
    }
}

public sealed class KyrolusInMemoryJobLockProvider : IKyrolusJobLockProvider
{
    private sealed class LockReleaser(Action onRelease) : IAsyncDisposable
    {
        private int _disposed;
        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                onRelease();
            }
            return ValueTask.CompletedTask;
        }
    }

    private readonly ConcurrentDictionary<string, DateTimeOffset> _activeLocks = new();

    public ValueTask<IAsyncDisposable?> TryAcquireLockAsync(string lockKey, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiration = now + lockDuration;

        var acquired = _activeLocks.AddOrUpdate(
            lockKey,
            _ => expiration,
            (_, existingExp) => existingExp <= now ? expiration : existingExp);

        if (acquired == expiration)
        {
            IAsyncDisposable releaser = new LockReleaser(() => _activeLocks.TryRemove(lockKey, out _));
            return ValueTask.FromResult<IAsyncDisposable?>(releaser);
        }

        return ValueTask.FromResult<IAsyncDisposable?>(null);
    }
}

public sealed class KyrolusJobScheduler : IKyrolusJobScheduler
{
    private readonly List<KyrolusJobScheduleRegistration> _jobs = [];

    public void ScheduleCronJob<TJob>(string cronExpression, string? jobName = null, bool useDistributedLock = true) where TJob : class, IKyrolusJob
    {
        _jobs.Add(new KyrolusJobScheduleRegistration
        {
            JobName = jobName ?? typeof(TJob).Name,
            JobType = typeof(TJob),
            CronExpression = cronExpression,
            UseDistributedLock = useDistributedLock
        });
    }

    public IReadOnlyList<KyrolusJobScheduleRegistration> GetRegisteredJobs() => _jobs.AsReadOnly();
}

public sealed class KyrolusJobSchedulerBackgroundService(
    IServiceProvider serviceProvider,
    IKyrolusJobScheduler scheduler,
    IKyrolusJobLockProvider lockProvider,
    ILogger<KyrolusJobSchedulerBackgroundService>? logger = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger?.LogInformation("Kyrolus Job Scheduler Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var registered = scheduler.GetRegisteredJobs();
            var now = DateTimeOffset.UtcNow;

            foreach (var jobReg in registered)
            {
                var nextRun = KyrolusCronParser.GetNextOccurrence(jobReg.CronExpression, now.AddMinutes(-1));
                if (nextRun.HasValue && Math.Abs((nextRun.Value - now).TotalSeconds) < 60)
                {
                    _ = ExecuteJobSafeAsync(jobReg, stoppingToken);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ExecuteJobSafeAsync(KyrolusJobScheduleRegistration jobReg, CancellationToken cancellationToken)
    {
        IAsyncDisposable? jobLock = null;
        try
        {
            if (jobReg.UseDistributedLock)
            {
                jobLock = await lockProvider.TryAcquireLockAsync($"job:{jobReg.JobName}", jobReg.LockDuration, cancellationToken).ConfigureAwait(false);
                if (jobLock is null)
                {
                    logger?.LogDebug("Skipping job {JobName} execution - lock held by another cluster instance.", jobReg.JobName);
                    return;
                }
            }

            using var scope = serviceProvider.CreateScope();
            if (scope.ServiceProvider.GetService(jobReg.JobType) is IKyrolusJob jobInstance)
            {
                var context = new KyrolusJobExecutionContext
                {
                    JobName = jobReg.JobName,
                    ScheduledFireTimeUtc = DateTimeOffset.UtcNow,
                    ActualFireTimeUtc = DateTimeOffset.UtcNow,
                    CancellationToken = cancellationToken
                };

                await jobInstance.ExecuteAsync(context).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "Error executing scheduled job {JobName}.", jobReg.JobName);
        }
        finally
        {
            if (jobLock != null)
            {
                await jobLock.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusScheduling(this IServiceCollection services, Action<IKyrolusJobScheduler>? configure = null)
    {
        var scheduler = new KyrolusJobScheduler();
        configure?.Invoke(scheduler);

        services.AddSingleton<IKyrolusJobScheduler>(scheduler);
        services.AddSingleton<IKyrolusJobLockProvider, KyrolusInMemoryJobLockProvider>();
        services.AddHostedService<KyrolusJobSchedulerBackgroundService>();
        return services;
    }
}
