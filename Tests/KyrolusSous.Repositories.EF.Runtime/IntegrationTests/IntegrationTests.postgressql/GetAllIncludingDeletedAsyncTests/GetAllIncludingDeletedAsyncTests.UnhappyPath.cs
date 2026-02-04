namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.GetAllIncludingDeletedAsyncTests;

public partial class GetAllIncludingDeletedAsyncTests
{
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

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid filter property throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidFilterProperty_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("NotARealProperty", "eq", "1")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync empty filter operator throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_EmptyFilterOperator_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("Name", "", "x")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid numeric filter value throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidNumericFilterValue_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("StockQuantity", "eq", "NotANumber")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid Guid filter value throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidGuidFilterValue_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("Id", "eq", "NotAGuid")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid DateTimeOffset filter value throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidDateTimeOffsetFilterValue_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("CreatedAt", "eq", "NotADateTimeOffset")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid bool filter value throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidBoolFilterValue_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.Build(new QueryRequest(Filters: [new FilterClause("IsActive", "eq", "NotABool")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync invalid orderBy property throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_InvalidOrderByProperty_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.BuildOrderBy(new QueryRequest(OrderBy: [new OrderClause("NotARealProperty")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync empty orderBy property throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_EmptyOrderByProperty_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.BuildOrderBy(new QueryRequest(OrderBy: [new OrderClause("")]));
        });
    }

    [Fact(DisplayName = "GetAllIncludingDeletedAsync null orderBy property throws from QueryHelper")]
    public void GetAllIncludingDeletedAsync_NullOrderByProperty_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var helper = scope.ServiceProvider.GetRequiredService<IQueryHelper<Product>>();

        Should.Throw<ArgumentException>(() =>
        {
            helper.BuildOrderBy(new QueryRequest(OrderBy: [new OrderClause(null!)]));
        });
    }
}
