namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.DisposeTests;

public partial class DisposeTests
{
    public static TheoryData<string, bool> ExecuteDisposeCases => new()
    {
        { "dispose-with-transaction", true },
        { "dispose-async-with-transaction", true },
        { "dispose-without-transaction", false },
        { "dispose-async-without-transaction", false }
    };

    [Theory(DisplayName = "ExecuteAsync after dispose throws with transaction and returns failed without transaction")]
    [MemberData(nameof(ExecuteDisposeCases))]
    public async Task UnitOfWork_Dispose_ExecuteAsync_Behavior(string caseId, bool useTransaction)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        if (caseId.StartsWith("dispose-async", StringComparison.Ordinal))
            await uow.DisposeAsync();
        else
            uow.Dispose();

        if (useTransaction)
        {
            await Should.ThrowAsync<ObjectDisposedException>(async () =>
                await uow.ExecuteAsync(() => Task.CompletedTask, useTransaction: true));
            return;
        }

        var result = await uow.ExecuteAsync(() => Task.CompletedTask, useTransaction: false);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<ObjectDisposedException>();
        result.Value.ShouldBe(0);
    }
}
