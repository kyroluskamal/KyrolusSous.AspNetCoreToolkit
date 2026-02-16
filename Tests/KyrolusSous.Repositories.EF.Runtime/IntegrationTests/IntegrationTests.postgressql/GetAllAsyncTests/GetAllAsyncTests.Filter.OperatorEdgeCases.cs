namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllAsyncTests;

public partial class GetAllAsyncTests
{
    private sealed record OperatorEdgeSuccessSpec(
        Type EntityType,
        QueryRequest Request,
        Action<IReadOnlyList<object>> Assert);

    private sealed record OperatorEdgeErrorSpec(QueryRequest Request, HttpStatusCode StatusCode, string MessageContains);

    private static readonly IReadOnlyDictionary<string, OperatorEdgeSuccessSpec> OperatorEdgeSuccessSpecs = BuildOperatorEdgeSuccessSpecs();
    private static readonly IReadOnlyDictionary<string, OperatorEdgeErrorSpec> OperatorEdgeErrorSpecs = BuildOperatorEdgeErrorSpecs();

    public static TheoryData<string> OperatorEdgeSuccessCases => CaseIdsFrom(OperatorEdgeSuccessSpecs);
    public static TheoryData<string> OperatorEdgeErrorCases => CaseIdsFrom(OperatorEdgeErrorSpecs);

    [Theory(DisplayName = "GetAllAsync handles edge operator cases")]
    [MemberData(nameof(OperatorEdgeSuccessCases))]
    public async Task GetAllAsync_Filter_OperatorEdgeCases_Work(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = OperatorEdgeSuccessSpecs[caseId];

        if (spec.EntityType == typeof(Product))
        {
            var items = await GetOkListAsync<Product>(spec.Request);
            spec.Assert(items.Cast<object>().ToList());
            return;
        }

        if (spec.EntityType == typeof(Payment))
        {
            var items = await GetOkListAsync<Payment>(spec.Request);
            spec.Assert(items.Cast<object>().ToList());
            return;
        }

        throw new InvalidOperationException($"Unsupported entity type '{spec.EntityType.Name}' in case '{caseId}'.");
    }

    [Theory(DisplayName = "GetAllAsync rejects invalid edge operator cases")]
    [MemberData(nameof(OperatorEdgeErrorCases))]
    public async Task GetAllAsync_Filter_OperatorEdgeCases_UnhappyPath(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        var spec = OperatorEdgeErrorSpecs[caseId];

        var (response, content) = await GetErrorAsync<Product>(spec.Request);
        response.StatusCode.ShouldBe(spec.StatusCode);
        content.ShouldNotBeNull();
        content.ShouldContain(spec.MessageContains);
    }

    private static IReadOnlyDictionary<string, OperatorEdgeSuccessSpec> BuildOperatorEdgeSuccessSpecs()
        => new Dictionary<string, OperatorEdgeSuccessSpec>
        {
            ["enum-eq-payment-status"] = new(
                typeof(Payment),
                new QueryRequest(Filters: [new FilterClause(nameof(Payment.Status), "eq", PaymentStatus.Paid.ToString())]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Payment)items[0]).Status.ShouldBe(PaymentStatus.Paid);
                }),

            ["enum-in-payment-status"] = new(
                typeof(Payment),
                new QueryRequest(Filters: [new FilterClause(nameof(Payment.Status), "in", "Paid,Failed")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Payment)items[0]).Status.ShouldBe(PaymentStatus.Paid);
                }),

            ["in-empty-list-yields-empty"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Name), "in", "   ")]),
                items => items.Count.ShouldBe(0)),

            ["any-uses-eq-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating eq 5")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Laptop Pro 15");
                }),

            ["any-uses-neq-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating neq 5")]),
                items =>
                {
                    items.Count.ShouldBe(2);
                    items.Cast<Product>().All(x => x.Name != "Laptop Pro 15").ShouldBeTrue();
                }),

            ["any-uses-gt-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating gt 4")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Laptop Pro 15");
                }),

            ["any-uses-gte-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating gte 4")]),
                items =>
                {
                    var names = items.Cast<Product>().Select(x => x.Name).OrderBy(x => x).ToArray();
                    names.ShouldBe(["Clean Code", "Laptop Pro 15"]);
                }),

            ["any-uses-lt-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating lt 4")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Noise Cancelling Headphones");
                }),

            ["any-uses-lte-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating lte 4")]),
                items =>
                {
                    var names = items.Cast<Product>().Select(x => x.Name).OrderBy(x => x).ToArray();
                    names.ShouldBe(["Clean Code", "Noise Cancelling Headphones"]);
                }),

            ["any-uses-contains-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Comment contains read")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Clean Code");
                }),

            ["any-uses-startswith-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Comment startswith Great")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Laptop Pro 15");
                }),

            ["any-uses-endswith-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Comment endswith concepts.")]),
                items =>
                {
                    items.Count.ShouldBe(1);
                    ((Product)items[0]).Name.ShouldBe("Clean Code");
                }),

            ["any-uses-between-keyword"] = new(
                typeof(Product),
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating between 4..5")]),
                items =>
                {
                    var names = items.Cast<Product>().Select(x => x.Name).OrderBy(x => x).ToArray();
                    names.ShouldBe(["Clean Code", "Laptop Pro 15"]);
                })
        };

    private static IReadOnlyDictionary<string, OperatorEdgeErrorSpec> BuildOperatorEdgeErrorSpecs()
        => new Dictionary<string, OperatorEdgeErrorSpec>
        {
            ["contains-null-value"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Name), "contains", null)]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["eq-null-on-nonnullable"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "eq", null)]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["orderby-dot-path"] = new(
                new QueryRequest(OrderBy: [new OrderClause(".")]),
                HttpStatusCode.InternalServerError,
                "Invalid orderBy"),

            ["in-leading-separator-null-token"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "in", ",25")]),
                HttpStatusCode.InternalServerError,
                "does not support NULL"),

            ["any-raw-comma-without-property"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "4,5")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["any-raw-in-keyword"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "Rating in 3,4")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter"),

            ["any-raw-dots-without-property"] = new(
                new QueryRequest(Filters: [new FilterClause(nameof(Product.Reviews), "any", "4..5")]),
                HttpStatusCode.InternalServerError,
                "Invalid filter")
        };
}
