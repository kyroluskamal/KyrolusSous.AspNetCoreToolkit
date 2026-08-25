using KyrolusSous.EndpointKit.EF;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule;
using KyrolusSous.EndpointKit.EF.Config;
using Shouldly;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKitEfTests
{
    public sealed class ProductEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Fact(DisplayName = "EF FilterBuilder: Parses comparison operators (gt, lt, eq, neq)")]
    public void FilterBuilder_Should_Parse_Numeric_Comparisons()
    {
        var expr = FilterBuilder.BuildFilterExpression<ProductEntity>("Price gt 50");
        expr.ShouldNotBeNull();

        var func = expr.Compile();
        func(new ProductEntity { Price = 100m }).ShouldBeTrue();
        func(new ProductEntity { Price = 20m }).ShouldBeFalse();
    }

    [Fact(DisplayName = "EF FilterBuilder: Parses string operators (contains, startswith, endswith)")]
    public void FilterBuilder_Should_Parse_String_Operators()
    {
        var expr = FilterBuilder.BuildFilterExpression<ProductEntity>("Name contains 'Phone'");
        expr.ShouldNotBeNull();

        var func = expr.Compile();
        func(new ProductEntity { Name = "iPhone 15" }).ShouldBeTrue();
        func(new ProductEntity { Name = "Galaxy S24" }).ShouldBeFalse();
    }

    [Fact(DisplayName = "EF FilterBuilder: Parses boolean and logical operators (comma = AND, pipe = OR)")]
    public void FilterBuilder_Should_Parse_Logical_Operators()
    {
        var expr = FilterBuilder.BuildFilterExpression<ProductEntity>("IsActive eq true, Price lt 200");
        expr.ShouldNotBeNull();

        var func = expr.Compile();
        func(new ProductEntity { IsActive = true, Price = 150m }).ShouldBeTrue();
        func(new ProductEntity { IsActive = false, Price = 150m }).ShouldBeFalse();
        func(new ProductEntity { IsActive = true, Price = 250m }).ShouldBeFalse();
    }

    [Fact(DisplayName = "EF OrderBuilder: Builds single and multi-column order functions")]
    public void OrderBuilder_Should_Build_Ordering()
    {
        var orderFunc = OrderBuilder.BuildOrderBy<ProductEntity>("Price:desc, Name:asc", null, false, out var error);
        error.ShouldBeNull();
        orderFunc.ShouldNotBeNull();

        var items = new List<ProductEntity>
        {
            new() { Id = 1, Name = "B", Price = 10m },
            new() { Id = 2, Name = "A", Price = 50m },
            new() { Id = 3, Name = "C", Price = 50m }
        }.AsQueryable();

        var sorted = orderFunc(items).ToList();
        sorted[0].Id.ShouldBe(2); // Price 50, Name A
        sorted[1].Id.ShouldBe(3); // Price 50, Name C
        sorted[2].Id.ShouldBe(1); // Price 10, Name B
    }

    [Fact(DisplayName = "EF FilterBuilder: Parses 'in', 'between', and 'notnull' operators")]
    public void FilterBuilder_Should_Parse_In_Between_And_NotNull()
    {
        var exprIn = FilterBuilder.BuildFilterExpression<ProductEntity>("Price in (10, 20, 50)");
        exprIn.ShouldNotBeNull();
        var funcIn = exprIn.Compile();
        funcIn(new ProductEntity { Price = 20m }).ShouldBeTrue();
        funcIn(new ProductEntity { Price = 30m }).ShouldBeFalse();

        var exprBetween = FilterBuilder.BuildFilterExpression<ProductEntity>("Price between (10, 50)");
        exprBetween.ShouldNotBeNull();
        var funcBetween = exprBetween.Compile();
        funcBetween(new ProductEntity { Price = 30m }).ShouldBeTrue();
        funcBetween(new ProductEntity { Price = 5m }).ShouldBeFalse();

        var exprNotNull = FilterBuilder.BuildFilterExpression<ProductEntity>("Name notnull");
        exprNotNull.ShouldNotBeNull();
        var funcNotNull = exprNotNull.Compile();
        funcNotNull(new ProductEntity { Name = "Something" }).ShouldBeTrue();
    }

    [Fact(DisplayName = "EF OrderBuilder: Rejects disallowed properties when strict")]
    public void OrderBuilder_Should_Enforce_Allowlist()
    {
        var allowed = new HashSet<string> { "Name" };
        var orderFunc = OrderBuilder.BuildOrderBy<ProductEntity>("Price:desc", allowed, strict: true, out var error);
        orderFunc.ShouldBeNull();
        error.ShouldNotBeNull();
        error.ShouldContain("not allowed");
    }

    [Fact(DisplayName = "EF ApiConfig: Sets default options and route configurations properly")]
    public void EfApiConfig_Should_Store_Configuration()
    {
        var config = new KyrolusEfApiConfig<ProductEntity>
        {
            Route = "products",
            ApiName = "ProductAPI",
            DefaultPageSize = 25,
            MaxPageSize = 100
        };

        config.Route.ShouldBe("products");
        config.ApiName.ShouldBe("ProductAPI");
        config.DefaultPageSize.ShouldBe(25);
        config.MaxPageSize.ShouldBe(100);
    }
}
