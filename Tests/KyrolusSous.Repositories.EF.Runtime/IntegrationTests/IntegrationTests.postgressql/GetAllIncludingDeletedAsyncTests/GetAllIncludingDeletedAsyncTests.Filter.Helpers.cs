namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    protected enum EntityKind
    {
        Product,
        Review
    }

    protected sealed record EntitySpec(
        EntityKind Kind,
        QueryRequest? Request,
        Action<List<Product>>? AssertProducts,
        Action<List<Review>>? AssertReviews);

    protected Task AssertProducts(QueryRequest? request, Action<List<Product>> assert)
        => WithSoftDeletedAsync_SingleKey<Product>(
            DataSeeder.productLaptopId,
            async (_, products, _, _, _) =>
            {
                products.ShouldNotBeNull();
                assert(products);
            },
            request);

    protected Task AssertReviews(QueryRequest? request, Action<List<Review>> assert)
        => WithSoftDeletedAsync_CompositeKey<Review>(
            DataSeeder.ReviewLapTopKey,
            async (_, reviews, _, _, _) =>
            {
                reviews.ShouldNotBeNull();
                assert(reviews);
            },
            request);

    protected static QueryRequest WithFilters(params FilterClause[] filters)
        => new(Filters: [.. filters]);

    protected static TheoryData<string> CaseIdsFrom<TSpec>(IReadOnlyDictionary<string, TSpec> specs)
    {
        var data = new TheoryData<string>();
        foreach (var key in specs.Keys)
            data.Add(key);
        return data;
    }

    protected Task RunEntityCase(IReadOnlyDictionary<string, EntitySpec> specs, string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = specs[caseId];
        return RunEntitySpec(spec);
    }

    protected Task RunEntitySpec(EntitySpec spec)
        => spec.Kind switch
        {
            EntityKind.Product => AssertProducts(spec.Request, products => spec.AssertProducts?.Invoke(products)),
            EntityKind.Review => AssertReviews(spec.Request, reviews => spec.AssertReviews?.Invoke(reviews)),
            _ => Task.CompletedTask
        };
}
