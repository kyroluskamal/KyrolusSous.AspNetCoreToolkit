namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter
{
    public sealed record InvalidOperatorCase(
        string Property,
        string Operator,
        string? Value,
        string? MessageContains = null);

    public static TheoryData<InvalidOperatorCase> OperatorInvalidCasesForProduct => new()
    {
        new InvalidOperatorCase(nameof(Product.StockQuantity), "eq", "NotANumber"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "neq", "NotANumber"),
        new InvalidOperatorCase(nameof(Product.Name), "gt", "Alpha"),
        new InvalidOperatorCase(nameof(Product.Name), "gte", "Alpha"),
        new InvalidOperatorCase(nameof(Product.Name), "lt", "Alpha"),
        new InvalidOperatorCase(nameof(Product.Name), "lte", "Alpha"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "contains", "2"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "startswith", "2"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "endswith", "2"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "isnull", null, "supported only for nullable"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "notnull", null, "supported only for nullable"),
        new InvalidOperatorCase(nameof(Product.StockQuantity), "in", "null,25", "does not support NULL"),
        new InvalidOperatorCase(nameof(Product.Id), "in", "NotAGuid", "could not be converted"),
        new InvalidOperatorCase(nameof(Product.Name), "any", "A"),
        new InvalidOperatorCase(nameof(Product.Name), "all", "A")
    };

    public static TheoryData<InvalidOperatorCase> OperatorInvalidCasesForReview => new()
    {
        new InvalidOperatorCase(nameof(Review.Rating), "eq", "NotANumber"),
        new InvalidOperatorCase(nameof(Review.Rating), "neq", "NotANumber"),
        new InvalidOperatorCase(nameof(Review.Comment), "gt", "Alpha"),
        new InvalidOperatorCase(nameof(Review.Comment), "gte", "Alpha"),
        new InvalidOperatorCase(nameof(Review.Comment), "lt", "Alpha"),
        new InvalidOperatorCase(nameof(Review.Comment), "lte", "Alpha"),
        new InvalidOperatorCase(nameof(Review.Rating), "contains", "1"),
        new InvalidOperatorCase(nameof(Review.Rating), "startswith", "1"),
        new InvalidOperatorCase(nameof(Review.Rating), "endswith", "1"),
        new InvalidOperatorCase(nameof(Review.Rating), "isnull", null, "supported only for nullable"),
        new InvalidOperatorCase(nameof(Review.Rating), "notnull", null, "supported only for nullable"),
        new InvalidOperatorCase(nameof(Review.Rating), "in", "null,3", "does not support NULL"),
        new InvalidOperatorCase(nameof(Review.ProductId), "in", "NotAGuid", "could not be converted"),
        new InvalidOperatorCase(nameof(Review.Comment), "any", "A"),
        new InvalidOperatorCase(nameof(Review.Comment), "all", "A")
    };

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid operators for single key entities")]
    [MemberData(nameof(OperatorInvalidCasesForProduct))]
    public void GetAllIncludingDeletedAsync_InvalidOperators_Throw_ForSingleKey(InvalidOperatorCase testCase)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        var ex = Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause(testCase.Property, testCase.Operator, testCase.Value)]));
        });

        if (!string.IsNullOrWhiteSpace(testCase.MessageContains))
            ex.Message.ShouldContain(testCase.MessageContains);
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync rejects invalid operators for composite key entities")]
    [MemberData(nameof(OperatorInvalidCasesForReview))]
    public void GetAllIncludingDeletedAsync_InvalidOperators_Throw_ForComposite(InvalidOperatorCase testCase)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Review>>();

        var ex = Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause(testCase.Property, testCase.Operator, testCase.Value)]));
        });

        if (!string.IsNullOrWhiteSpace(testCase.MessageContains))
            ex.Message.ShouldContain(testCase.MessageContains);
    }
}
