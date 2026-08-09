namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperAnyAllTests
{
    [Fact]
    public void BuildFilter_AnyOperator_Works_WithNestedFilter()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Items", "any", $"CategoryId = {RuntimeQueryHelperTestData.CategoryA}")]));

        result.Count.ShouldBe(2);
        result.All(x => x.Items.Any(i => i.CategoryId == RuntimeQueryHelperTestData.CategoryA)).ShouldBeTrue();
    }

    [Fact]
    public void BuildFilter_AllOperator_Works_WithNestedFilter()
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Items", "all", "Rating >= 4")]));

        result.Count.ShouldBe(2);
        result.All(x => x.Items.All(i => i.Rating >= 4)).ShouldBeTrue();
    }

    [Fact]
    public void BuildFilter_AnyOperator_Works_WithValueList()
    {
        Should.Throw<ArgumentException>(() =>
            RuntimeQueryHelperTestData.ApplyFilter(
                new QueryRequest(Filters: [new FilterClause("Scores", "any", "2,5")])));
    }
}
