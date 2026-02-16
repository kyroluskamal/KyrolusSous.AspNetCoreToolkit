namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.DisposeTests;

public partial class DisposeTests(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed record DisposeSpec(Func<IKyrolusUnitOfWork, Task> DisposeAction);

    private static readonly IReadOnlyDictionary<string, DisposeSpec> DisposeSpecs = BuildDisposeSpecs();
    public static TheoryData<string> DisposeCases => CaseIdsFrom(DisposeSpecs);

    [Theory(DisplayName = "UnitOfWork dispose is idempotent and SaveChangesAsync throws after dispose")]
    [MemberData(nameof(DisposeCases))]
    public async Task UnitOfWork_Dispose_SaveChangesAsync_Throws(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = DisposeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        await spec.DisposeAction(uow);
        await spec.DisposeAction(uow);

        await Should.ThrowAsync<ObjectDisposedException>(async () => await uow.SaveChangesAsync());
    }

    [Theory(DisplayName = "UnitOfWork dispose causes SaveChangesWithRetryAsync to return failed")]
    [MemberData(nameof(DisposeCases))]
    public async Task UnitOfWork_Dispose_SaveChangesWithRetryAsync_ReturnsFailed(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = DisposeSpecs[caseId];
        using var scope = Factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IKyrolusUnitOfWork>();

        await spec.DisposeAction(uow);

        var result = await uow.SaveChangesWithRetryAsync();
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<ObjectDisposedException>();
        result.Value.ShouldBe(0);
        result.Concurrency.ShouldBeNull();
    }

    private static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    private static IReadOnlyDictionary<string, DisposeSpec> BuildDisposeSpecs()
        => new Dictionary<string, DisposeSpec>
        {
            ["dispose"] = new(uow =>
            {
                uow.Dispose();
                return Task.CompletedTask;
            }),
            ["dispose-async"] = new(uow => uow.DisposeAsync().AsTask())
        };
}
