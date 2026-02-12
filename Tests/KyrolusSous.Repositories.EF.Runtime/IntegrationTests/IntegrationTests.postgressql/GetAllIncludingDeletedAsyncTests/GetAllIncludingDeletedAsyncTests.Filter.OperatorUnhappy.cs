namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    private sealed record InvalidOperatorSpec(
        string Property,
        string Operator,
        string? Value,
        string? MessageContains);

    private static readonly IReadOnlyDictionary<string, InvalidOperatorSpec> InvalidSingleKeyOperatorSpecs = BuildInvalidSingleKeyOperatorSpecs();
    private static readonly IReadOnlyDictionary<string, InvalidOperatorSpec> InvalidCompositeKeyOperatorSpecs = BuildInvalidCompositeKeyOperatorSpecs();

    public static TheoryData<string> InvalidSingleKeyOperatorCases => CaseIdsFrom(InvalidSingleKeyOperatorSpecs);
    public static TheoryData<string> InvalidCompositeKeyOperatorCases => CaseIdsFrom(InvalidCompositeKeyOperatorSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid operators for single-key entities")]
    [MemberData(nameof(InvalidSingleKeyOperatorCases))]
    public async Task GetAllIncludingDeletedAsync_InvalidOperators_ReturnError_ForSingleKey(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidSingleKeyOperatorSpecs[caseId];
        var request = new QueryRequest(
            Filters: [new FilterClause(spec.Property, spec.Operator, spec.Value)],
            IncludeDeleted: true);

        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Product>(request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content.ShouldNotBeNull();
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains);
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid operators for composite-key entities")]
    [MemberData(nameof(InvalidCompositeKeyOperatorCases))]
    public async Task GetAllIncludingDeletedAsync_InvalidOperators_ReturnError_ForCompositeKey(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = InvalidCompositeKeyOperatorSpecs[caseId];
        var request = new QueryRequest(
            Filters: [new FilterClause(spec.Property, spec.Operator, spec.Value)],
            IncludeDeleted: true);

        var (response, _, content) = await ArrangeAndActUseingHttpForListAsync<Review>(request);
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        content.ShouldNotBeNull();
        if (!string.IsNullOrWhiteSpace(spec.MessageContains))
            content.ShouldContain(spec.MessageContains);
    }

    private static IReadOnlyDictionary<string, InvalidOperatorSpec> BuildInvalidSingleKeyOperatorSpecs()
        => new Dictionary<string, InvalidOperatorSpec>
        {
            ["stockquantity-eq-notnumber"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "eq", "NotANumber", null),
            ["stockquantity-neq-notnumber"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "neq", "NotANumber", null),
            ["stockquantity-eq-null"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "eq", "null", "cannot use NULL"),
            ["stockquantity-eq-null-symbol"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "=", "null", "cannot use NULL"),
            ["stockquantity-eq-null-symbol2"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "==", "null", "cannot use NULL"),
            ["name-gt"] = new InvalidOperatorSpec(nameof(Product.Name), "gt", "Alpha", null),
            ["name-gte"] = new InvalidOperatorSpec(nameof(Product.Name), "gte", "Alpha", null),
            ["name-lt"] = new InvalidOperatorSpec(nameof(Product.Name), "lt", "Alpha", null),
            ["name-lte"] = new InvalidOperatorSpec(nameof(Product.Name), "lte", "Alpha", null),
            ["stockquantity-contains"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "contains", "2", null),
            ["stockquantity-startswith"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "startswith", "2", null),
            ["stockquantity-endswith"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "endswith", "2", null),
            ["stockquantity-isnull"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "isnull", null, "supported only for nullable"),
            ["stockquantity-notnull"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "notnull", null, "supported only for nullable"),
            ["stockquantity-in-null"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "in", "null,25", "does not support NULL"),
            ["id-in-notguid"] = new InvalidOperatorSpec(nameof(Product.Id), "in", "NotAGuid", "could not be converted"),
            ["id-contains"] = new InvalidOperatorSpec(nameof(Product.Id), "contains", DataSeeder.productLaptopId.ToString(), "Unsupported operator"),
            ["id-between"] = new InvalidOperatorSpec(nameof(Product.Id), "between", $"{DataSeeder.productLaptopId}..{DataSeeder.productHeadphonesId}", "Invalid filter"),
            ["finishedat-gt"] = new InvalidOperatorSpec(nameof(Product.FinishedAt), "gt", "1.00:00:00", "Invalid filter"),
            ["isactive-between"] = new InvalidOperatorSpec(nameof(Product.IsActive), "between", "true,false", "Invalid filter"),
            ["name-any"] = new InvalidOperatorSpec(nameof(Product.Name), "any", "A", null),
            ["name-all"] = new InvalidOperatorSpec(nameof(Product.Name), "all", "A", null),
            ["stockquantity-in-invalid"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "in", "NotANumber", "could not be converted"),
            ["stockquantity-between-invalid"] = new InvalidOperatorSpec(nameof(Product.StockQuantity), "between", "NotANumber..20", "Invalid filter")
        };

    private static IReadOnlyDictionary<string, InvalidOperatorSpec> BuildInvalidCompositeKeyOperatorSpecs()
        => new Dictionary<string, InvalidOperatorSpec>
        {
            ["rating-eq-notnumber"] = new InvalidOperatorSpec(nameof(Review.Rating), "eq", "NotANumber", null),
            ["rating-neq-notnumber"] = new InvalidOperatorSpec(nameof(Review.Rating), "neq", "NotANumber", null),
            ["rating-eq-null"] = new InvalidOperatorSpec(nameof(Review.Rating), "eq", "null", "cannot use NULL"),
            ["rating-eq-null-symbol"] = new InvalidOperatorSpec(nameof(Review.Rating), "=", "null", "cannot use NULL"),
            ["rating-eq-null-symbol2"] = new InvalidOperatorSpec(nameof(Review.Rating), "==", "null", "cannot use NULL"),
            ["comment-gt"] = new InvalidOperatorSpec(nameof(Review.Comment), "gt", "Alpha", null),
            ["comment-gte"] = new InvalidOperatorSpec(nameof(Review.Comment), "gte", "Alpha", null),
            ["comment-lt"] = new InvalidOperatorSpec(nameof(Review.Comment), "lt", "Alpha", null),
            ["comment-lte"] = new InvalidOperatorSpec(nameof(Review.Comment), "lte", "Alpha", null),
            ["rating-contains"] = new InvalidOperatorSpec(nameof(Review.Rating), "contains", "1", null),
            ["rating-startswith"] = new InvalidOperatorSpec(nameof(Review.Rating), "startswith", "1", null),
            ["rating-endswith"] = new InvalidOperatorSpec(nameof(Review.Rating), "endswith", "1", null),
            ["rating-isnull"] = new InvalidOperatorSpec(nameof(Review.Rating), "isnull", null, "supported only for nullable"),
            ["rating-notnull"] = new InvalidOperatorSpec(nameof(Review.Rating), "notnull", null, "supported only for nullable"),
            ["rating-in-null"] = new InvalidOperatorSpec(nameof(Review.Rating), "in", "null,3", "does not support NULL"),
            ["productid-in-notguid"] = new InvalidOperatorSpec(nameof(Review.ProductId), "in", "NotAGuid", "could not be converted"),
            ["productid-contains"] = new InvalidOperatorSpec(nameof(Review.ProductId), "contains", DataSeeder.productLaptopId.ToString(), "Unsupported operator"),
            ["productid-between"] = new InvalidOperatorSpec(nameof(Review.ProductId), "between", $"{DataSeeder.productLaptopId}..{DataSeeder.productHeadphonesId}", "Invalid filter"),
            ["finishedat-gt"] = new InvalidOperatorSpec(nameof(Review.FinishedAt), "gt", "1.00:00:00", "Invalid filter"),
            ["comment-any"] = new InvalidOperatorSpec(nameof(Review.Comment), "any", "A", null),
            ["comment-all"] = new InvalidOperatorSpec(nameof(Review.Comment), "all", "A", null),
            ["rating-between-invalid"] = new InvalidOperatorSpec(nameof(Review.Rating), "between", "NotANumber..5", "Invalid filter")
        };
}
