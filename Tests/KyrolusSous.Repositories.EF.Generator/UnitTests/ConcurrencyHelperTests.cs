using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class ConcurrencyHelperTests
{
    [Fact(DisplayName = "BuildConcurrencyInfoAsync returns null when no entries")]
    public async Task BuildConcurrencyInfoAsync_NoEntries_ReturnsNull()
    {
        var ex = new DbUpdateConcurrencyException();
        var info = await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex);
        info.ShouldBeNull();
    }

    [Fact(DisplayName = "ExecuteWithConcurrencyRetry returns success immediately")]
    public async Task ExecuteWithConcurrencyRetry_SuccessFirst()
    {
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1 };
        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            () => Task.FromResult(42),
            policy);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        result.Value.ShouldBe(42);
    }

    [Fact(DisplayName = "ExecuteWithConcurrencyRetry returns failed on general exception")]
    public async Task ExecuteWithConcurrencyRetry_FailedOnException()
    {
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1 };
        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync<int>(
            () => throw new InvalidOperationException("boom"),
            policy);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact(DisplayName = "ExecuteWithConcurrencyRetry returns conflict with enriched info after retries exhausted")]
    public async Task ExecuteWithConcurrencyRetry_ConflictAfterRetries()
    {
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1, ConcurrencyRetryDelay = TimeSpan.Zero };
        var ex = new DbUpdateConcurrencyException();
        var info = new ConcurrencyInfo(new byte[] { 1 }, new byte[] { 2 }, new Dictionary<string, object?> { ["A"] = 5 }, 0);

        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync<int>(
            () => throw ex,
            policy,
            _ => Task.FromResult<ConcurrencyInfo?>(info));

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
        result.Concurrency.ShouldNotBeNull();
        result.Concurrency!.Value.RetryCount.ShouldBe(1);
        result.Concurrency!.Value.OriginalRowVersion.ShouldBe(info.OriginalRowVersion);
        result.Concurrency!.Value.CurrentRowVersion.ShouldBe(info.CurrentRowVersion);
        result.Concurrency!.Value.DatabaseValues.ShouldContainKey("A");
    }
}
