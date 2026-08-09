namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperSetOperatorTests
{
    [Fact]
    public void BuildFilter_InOperator_Works_WithNulls_ForNullableTypes()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("NullableInt", "in", "null,3")]));

        result.Count.ShouldBe(2);
        result.Any(x => x.NullableInt is null).ShouldBeTrue();
        result.Any(x => x.NullableInt == 3).ShouldBeTrue();
    }

    [Fact]
    public void BuildFilter_InOperator_Works_ForGuid()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Id", "in", $"{RuntimeQueryHelperTestData.Id1},{RuntimeQueryHelperTestData.Id3}")]));

        result.Select(x => x.Id).OrderBy(x => x).ShouldBe([RuntimeQueryHelperTestData.Id1, RuntimeQueryHelperTestData.Id3]);
    }

    [Fact]
    public void BuildFilter_InOperator_Works_ForString_WithPipes()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", "in", "Alpha|Gamma")]));

        result.Select(x => x.Name).OrderBy(x => x).ShouldBe(["Alpha", "Gamma"]);
    }

    [Fact]
    public void BuildFilter_BetweenOperator_Works_ForDateTimeOffset()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("DateTimeOffsetValue", "between", "2024-06-01T00:00:00Z..2024-12-31T00:00:00Z")]));

        result.Count.ShouldBe(2);
        result.All(x => x.DateTimeOffsetValue.Year == 2024).ShouldBeTrue();
    }

    [Fact]
    public void BuildFilter_BetweenOperator_Works_ForTimeOnly()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("TimeOnlyValue", "between", "09:00..11:00")]));

        result.Count.ShouldBe(2);
        result.All(x => x.TimeOnlyValue >= new TimeOnly(9, 0) && x.TimeOnlyValue <= new TimeOnly(11, 0)).ShouldBeTrue();
    }
}
