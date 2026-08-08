namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public sealed record SoftDeleteCase(string CaseId, Func<GetAllIncludingDeletedAsyncTests, Task> Run);

    protected static SoftDeleteCase ProductCase(string caseId, Func<GetAllIncludingDeletedAsyncTests, Task> run)
        => new(caseId, run);

    protected static SoftDeleteCase ReviewCase(string caseId, Func<GetAllIncludingDeletedAsyncTests, Task> run)
        => new(caseId, run);

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected Task WithProductSoftDeleted(
        Func<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>, IServiceProvider, Task> body,
        QueryRequest? request = null,
        KyrolusRepositoryPolicy? policy = null)
        => WithSoftDeletedAsync_SingleKey<Product>(
            DataSeeder.productLaptopId,
            async (_, _, _, repo, sp) => await body(repo, sp!),
            request,
            policy);

    protected Task WithReviewSoftDeleted(
        Func<KyrolusCompositeKeySoftDeleteRepositoryAsync<ApplicationDbContext, Review>, IServiceProvider, Task> body,
        QueryRequest? request = null,
        KyrolusRepositoryPolicy? policy = null)
        => WithSoftDeletedAsync_CompositeKey<Review>(
            DataSeeder.ReviewLapTopKey,
            async (_, _, _, repo, sp) => await body(repo, sp!),
            request,
            policy);
}
