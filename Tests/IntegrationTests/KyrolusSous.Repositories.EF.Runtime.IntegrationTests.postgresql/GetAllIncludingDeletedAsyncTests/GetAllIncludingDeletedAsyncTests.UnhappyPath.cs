namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
    public static TheoryData<string, string, string?> InvalidFilterCases => new()
    {
        { "NotARealProperty", "eq", "1" },
        { "Name", "", "x" },
        { "StockQuantity", "eq", "NotANumber" },
        { "Id", "eq", "NotAGuid" },
        { "CreatedAt", "eq", "NotADateTimeOffset" },
        { "IsActive", "eq", "NotABool" }
    };

    public static TheoryData<string?> InvalidOrderByCases => new()
    {
        "NotARealProperty",
        "",
        null
    };

    [Fact(DisplayName = "GetAllIncludingDeletedAsync throws when include string is invalid navigation")]
    public async Task GetAllIncludingDeletedAsync_InvalidIncludeString_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<KyrolusSingleKeySoftDeleteRepositoryAsync<ApplicationDbContext, Product, Guid>>();

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await repo.GetAllIncludingDeletedAsync(
                includeProperties: ["NotARealNavigation"],
                asNoTracking: true,
                useSplitQuery: true,
                cancellationToken: default);
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync invalid filters throw from QueryHelper")]
    [MemberData(nameof(InvalidFilterCases))]
    public void GetAllIncludingDeletedAsync_InvalidFilters_Throw(string property, string op, string? value)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause(property, op, value)]));
        });
    }

    [Theory(DisplayName = "GetAllIncludingDeletedAsync invalid orderBy property throws from QueryHelper")]
    [MemberData(nameof(InvalidOrderByCases))]
    public void GetAllIncludingDeletedAsync_InvalidOrderByProperty_Throws(string? property)
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.BuildOrderBy(new QueryRequest(OrderBy: [new OrderClause(property!)]));
        });
    }
}
