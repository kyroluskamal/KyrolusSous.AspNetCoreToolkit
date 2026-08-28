using KyrolusSous.Scheduling.Abstractions;
using KyrolusSous.Scheduling.Core;
using Shouldly;
using Xunit;

namespace KyrolusSous.Scheduling.UnitTests;

public sealed class SchedulingTests
{
    private sealed class DummyCleanupJob : IKyrolusJob
    {
        public bool Executed { get; private set; }

        public Task ExecuteAsync(KyrolusJobExecutionContext context)
        {
            Executed = true;
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Cron Parser Calculates Next Interval Correctly")]
    public void CronParser_CalculatesNextInterval_Correctly()
    {
        var baseTime = new DateTimeOffset(2026, 8, 28, 12, 14, 30, TimeSpan.Zero);
        var next = KyrolusCronParser.GetNextOccurrence("*/5 * * * *", baseTime);

        next.ShouldNotBeNull();
        next.Value.Minute.ShouldBe(15);
        next.Value.Second.ShouldBe(0);
    }

    [Fact(DisplayName = "In Memory Job Lock Provider Prevents Concurrent Acquisition")]
    public async Task JobLockProvider_PreventsConcurrentAcquisition()
    {
        var lockProvider = new KyrolusInMemoryJobLockProvider();

        var lock1 = await lockProvider.TryAcquireLockAsync("report-generation", TimeSpan.FromMinutes(1));
        lock1.ShouldNotBeNull();

        var lock2 = await lockProvider.TryAcquireLockAsync("report-generation", TimeSpan.FromMinutes(1));
        lock2.ShouldBeNull(); // Already locked

        await lock1.DisposeAsync();

        var lock3 = await lockProvider.TryAcquireLockAsync("report-generation", TimeSpan.FromMinutes(1));
        lock3.ShouldNotBeNull(); // Successfully acquired after release
        await lock3.DisposeAsync();
    }

    [Fact(DisplayName = "Job Scheduler Registers Jobs Correctly")]
    public void JobScheduler_RegistersJobs_Correctly()
    {
        var scheduler = new KyrolusJobScheduler();
        scheduler.ScheduleCronJob<DummyCleanupJob>("0 0 * * *", "DailyDatabaseCleanup");

        var registered = scheduler.GetRegisteredJobs();
        registered.Count.ShouldBe(1);
        registered[0].JobName.ShouldBe("DailyDatabaseCleanup");
        registered[0].JobType.ShouldBe(typeof(DummyCleanupJob));
    }
}
