using KyrolusSous.Repositories.EF.Abstractions.Query;
using Shouldly;
using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class QueryPrimitivesTests
{
    [Fact(DisplayName = "TryParse returns default request on null or empty input")]
    public void TryParse_NullOrEmpty_ReturnsDefault()
    {
        QueryRequest.TryParse(null, null, out var resultNull).ShouldBeTrue();
        resultNull.Includes.ShouldBeNull();
        resultNull.Filters.ShouldBeNull();
        resultNull.OrderBy.ShouldBeNull();

        QueryRequest.TryParse(string.Empty, null, out var resultEmpty).ShouldBeTrue();
        resultEmpty.Includes.ShouldBeNull();
    }

    [Fact(DisplayName = "TryParse returns false on invalid json and provides default request")]
    public void TryParse_InvalidJson_ReturnsFalseWithDefault()
    {
        QueryRequest.TryParse("{bad json", null, out var result).ShouldBeFalse();
        result.Includes.ShouldBeNull();
        result.Filters.ShouldBeNull();
        result.OrderBy.ShouldBeNull();
    }

    [Fact(DisplayName = "Parse reads includes, filters, order, flags from json")]
    public void Parse_ValidJson_PopulatesRequest()
    {
        var json = """
        {
            "includes": ["Product", "Customer"],
            "filters": [
                { "property": "Name", "operator": "contains", "value": "soft" },
                { "property": "IsActive", "operator": "eq", "value": "true" }
            ],
            "orderBy": [
                { "property": "CreatedAt", "desc": true }
            ],
            "asNoTracking": true,
            "useSplitQuery": false
        }
        """;

        var request = QueryRequest.Parse(json, null);

        request.Includes.ShouldNotBeNull().Length.ShouldBe(2);
        request.Filters.ShouldNotBeNull().Length.ShouldBe(2);
        request.OrderBy.ShouldNotBeNull().Length.ShouldBe(1);
        request.AsNoTracking.ShouldBe(true);
        request.UseSplitQuery.ShouldBe(false);
    }

    [Fact(DisplayName = "Parse throws FormatException when TryParse fails")]
    public void Parse_InvalidJson_Throws()
    {
        Should.Throw<FormatException>(() => QueryRequest.Parse("{bad", null));
    }

    [Fact(DisplayName = "TryParse trims whitespace and returns default request")]
    public void TryParse_Whitespace_ReturnsDefault()
    {
        QueryRequest.TryParse("   ", null, out var result).ShouldBeTrue();
        result.Includes.ShouldBeNull();
        result.Filters.ShouldBeNull();
        result.OrderBy.ShouldBeNull();
    }

    [Fact(DisplayName = "QueryParts holds supplied values")]
    public void QueryParts_HoldsValues()
    {
        Expression<Func<DummyEntity, bool>> filter = e => e.IsActive;
        Func<IQueryable<DummyEntity>, IOrderedQueryable<DummyEntity>> order = q => q.OrderBy(e => e.Id);
        Expression<Func<DummyEntity, object?>>[] includes = [e => e.Name];

        var parts = new QueryParts<DummyEntity>(filter, order, includes, AsNoTracking: true, UseSplitQuery: true, IncludeGraph: null);

        parts.Filter.ShouldBe(filter);
        parts.OrderBy.ShouldBe(order);
        parts.Includes.ShouldBeOfType<Expression<Func<DummyEntity, object?>>[]>();
        parts.Includes.Length.ShouldBe(1);
        parts.AsNoTracking.ShouldBe(true);
        parts.UseSplitQuery.ShouldBe(true);
    }

    private sealed class DummyEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
