namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    public static TheoryData<string, bool> CancellationCases => new()
    {
        { "include-properties", false },
        { "include-expressions", true }
    };

    [Theory(DisplayName = "GetAllAsync respects cancellation token")]
    [MemberData(nameof(CancellationCases))]
    public async Task GetAllAsync_CanceledToken_ThrowsOperationCanceled(string caseId, bool useIncludeExpressions)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        if (useIncludeExpressions)
        {
            await Should.ThrowAsync<OperationCanceledException>(async () =>
            {
                await repo.GetAllAsync(
                    filter: null,
                    orderBy: null,
                    asNoTracking: true,
                    useSplitQuery: true,
                    cancellationToken: cts.Token,
                    includeExpressions: static p => p.Reviews);
            });
            return;
        }

        await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            await repo.GetAllAsync(
                includeProperties: ["Reviews"],
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: cts.Token);
        });
    }
}
