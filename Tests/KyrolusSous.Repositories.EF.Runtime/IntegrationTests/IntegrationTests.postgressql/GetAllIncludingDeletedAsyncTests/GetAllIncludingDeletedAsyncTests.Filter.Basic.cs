namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsync_Filter(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private static readonly IReadOnlyDictionary<string, EntitySpec> NoFilterSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product, null, p => p.Count.ShouldBe(3), null),
        ["review"] = new(EntityKind.Review, null, null, r => r.Count.ShouldBe(3))
    };

    public static TheoryData<string> NoFilterCases => CaseIdsFrom(NoFilterSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns all entities with no filters")]
    [MemberData(nameof(NoFilterCases))]
    public Task GetAllIncludingDeletedAsync_NoFilter_ReturnsAll(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(NoFilterSpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> OrderingSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Product.StockQuantity))]),
            p => p.Select(x => x.StockQuantity).ShouldBeInOrder(),
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating))]),
            null,
            r => r.Select(x => x.Rating).ShouldBeInOrder())
    };

    public static TheoryData<string> OrderingCases => CaseIdsFrom(OrderingSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with Assencding ordering")]
    [MemberData(nameof(OrderingCases))]
    public Task GetAllIncludingDeletedAsync_Ordering_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(OrderingSpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> DescOrderingSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Product.StockQuantity), true)]),
            p => p.Select(x => x.StockQuantity).ShouldBeInOrder(SortDirection.Descending),
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating), true)]),
            null,
            r => r.Select(x => x.Rating).ShouldBeInOrder(SortDirection.Descending))
    };

    public static TheoryData<string> DescOrderingCases => CaseIdsFrom(DescOrderingSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with descending ordering")]
    [MemberData(nameof(DescOrderingCases))]
    public Task GetAllIncludingDeletedAsync_DescendingOrdering_Works(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(DescOrderingSpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> MultiOrderBySpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Product.Price)), new OrderClause(nameof(Product.StockQuantity), true)]),
            p => p.ShouldBe([.. p.OrderBy(x => x.Price).ThenByDescending(x => x.StockQuantity)]),
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(OrderBy: [new OrderClause(nameof(Review.Rating)), new OrderClause(nameof(Review.CreatedAt), true)]),
            null,
            r => r.ShouldBe([.. r.OrderBy(x => x.Rating).ThenByDescending(x => x.CreatedAt)]))
    };

    public static TheoryData<string> MultiOrderByCases => CaseIdsFrom(MultiOrderBySpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync uses more that one OrderBy clause")]
    [MemberData(nameof(MultiOrderByCases))]
    public Task GetAllIncludingDeletedAsync_MultipleOrderBy_ReturnsEntitiesWithMultipleOrderBy(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(MultiOrderBySpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> FilterOrderIncludeSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(
                Filters: [new FilterClause(nameof(Product.StockQuantity), "gt", "25")],
                OrderBy: [new OrderClause(nameof(Product.StockQuantity))],
                Includes: [nameof(Product.Reviews), "", nameof(Product.OrderLines), nameof(Product.ProductCategories)],
                UseSplitQuery: true,
                AsNoTracking: true),
            p =>
            {
                p.Count.ShouldBe(2);
                p.All(x => x.StockQuantity > 25).ShouldBeTrue();
                p.Select(x => x.StockQuantity).ShouldBeInOrder();
                p[0].ProductCategories.ShouldNotBeNull();
                p[1].ProductCategories.ShouldNotBeNull();
                p[0].OrderLines.ShouldNotBeNull();
                p[1].OrderLines.ShouldNotBeNull();
                p[0].Reviews.ShouldNotBeNull();
                p[1].Reviews.ShouldNotBeNull();
            },
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(
                Filters: [new FilterClause(nameof(Review.Rating), "gt", "4")],
                OrderBy: [new OrderClause(nameof(Review.Rating))],
                Includes: [nameof(Review.Product), nameof(Review.Customer)],
                UseSplitQuery: true,
                AsNoTracking: true),
            null,
            r =>
            {
                r.Count.ShouldBe(1);
                r.All(x => x.Rating > 4).ShouldBeTrue();
                r.Select(x => x.Rating).ShouldBeInOrder();
                r[0].Product.ShouldNotBeNull();
                r[0].Customer.ShouldNotBeNull();
            })
    };

    public static TheoryData<string> FilterOrderIncludeCases => CaseIdsFrom(FilterOrderIncludeSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter, ordering and Include Properties")]
    [MemberData(nameof(FilterOrderIncludeCases))]
    public Task GetAllIncludingDeletedAsync_FilteringOrderingDefaultIncludeProperties_ReturnsEntitiesWithFilteringOrderingAndDefaultIncludeProperties(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(FilterOrderIncludeSpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> FilteringEmptySpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(Filters: [new FilterClause(nameof(Product.StockQuantity), "gt", "1000")]),
            p => p.Count.ShouldBe(0),
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), "gt", "10")]),
            null,
            r => r.Count.ShouldBe(0))
    };

    public static TheoryData<string> FilteringEmptyCases => CaseIdsFrom(FilteringEmptySpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync returns entities with gt Filter that results in no entities")]
    [MemberData(nameof(FilteringEmptyCases))]
    public Task GetAllIncludingDeletedAsync_Filtering_ReturnsNoEntities(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(FilteringEmptySpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> MultiFilterSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(Filters:
            [
                new FilterClause(nameof(Product.StockQuantity), "gt", "25"),
                new FilterClause(nameof(Product.Price), "lt", 50.ToString())
            ]),
            p =>
            {
                p.Count.ShouldBe(1);
                p[0].StockQuantity.ShouldBeGreaterThan(25);
                p[0].Price.ShouldBeLessThan(50);
            },
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(Filters: [new FilterClause(nameof(Review.Rating), "gt", "4")]),
            null,
            r =>
            {
                r.Count.ShouldBe(1);
                r[0].Rating.ShouldBeGreaterThan(4);
            })
    };

    public static TheoryData<string> MultiFilterCases => CaseIdsFrom(MultiFilterSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (gt and lt)")]
    [MemberData(nameof(MultiFilterCases))]
    public Task GetAllIncludingDeletedAsync_MultipleFilters_ReturnsEntitiesWithMultipleFilters(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(MultiFilterSpecs, caseId);
    }

    private static readonly IReadOnlyDictionary<string, EntitySpec> MultiFilterSymbolSpecs = new Dictionary<string, EntitySpec>
    {
        ["product"] = new(EntityKind.Product,
            new QueryRequest(Filters:
            [
                new FilterClause(nameof(Product.StockQuantity), ">", 25.ToString()),
                new FilterClause(nameof(Product.Price), "<", 50.ToString())
            ]),
            p =>
            {
                p.Count.ShouldBe(1);
                p[0].StockQuantity.ShouldBeGreaterThan(25);
                p[0].Price.ShouldBeLessThan(50);
            },
            null),
        ["review"] = new(EntityKind.Review,
            new QueryRequest(Filters:
            [
                new FilterClause(nameof(Review.Rating), ">", "3"),
                new FilterClause(nameof(Review.Rating), "<", "5")
            ]),
            null,
            r =>
            {
                r.Count.ShouldBe(1);
                r[0].Rating.ShouldBe(4);
            })
    };

    public static TheoryData<string> MultiFilterSymbolCases => CaseIdsFrom(MultiFilterSymbolSpecs);

    [Theory(DisplayName = "GetAllIncludingDeletedAsync should use multiple filters (> and <)")]
    [MemberData(nameof(MultiFilterSymbolCases))]
    public Task GetAllIncludingDeletedAsync_MultipleFilters__ReturnsEntitiesWithMultipleFilters(string caseId)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        return RunEntityCase(MultiFilterSymbolSpecs, caseId);
    }
}
