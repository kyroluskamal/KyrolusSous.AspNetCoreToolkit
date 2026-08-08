namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected async Task<List<TEntity>> GetOkListAsync<TEntity>(QueryRequest? request = null)
    {
        var (response, items, _) = await ArrangeAndActUseingHttpForListAsync<TEntity>(request);
        response.EnsureSuccessStatusCode();
        items.ShouldNotBeNull();
        return items!;
    }

    protected async Task<(HttpResponseMessage response, string? content)> GetErrorAsync<TEntity>(QueryRequest request)
    {
        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<TEntity>(request);
        return (response, content);
    }
}
