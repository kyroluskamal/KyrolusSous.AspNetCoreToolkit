namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperOrderByTests
{
    [Fact(DisplayName = "Build Order By Orders By Single Clause")]
    public void BuildOrderBy_OrdersBySingleClause()
    {
        var result = RuntimeQueryHelperTestData.ApplyOrderBy(
            new QueryRequest(OrderBy: [new OrderClause("IntValue")]));

        result.Select(x => x.IntValue).ShouldBeInOrder();
    }

    [Fact(DisplayName = "Build Order By Orders By Multiple Clauses")]
    public void BuildOrderBy_OrdersByMultipleClauses()
    {
        var result = RuntimeQueryHelperTestData.ApplyOrderBy(
            new QueryRequest(OrderBy: [new OrderClause("DecimalValue"), new OrderClause("IntValue", true)]));

        var sorted = result
            .OrderBy(x => x.DecimalValue)
            .ThenByDescending(x => x.IntValue)
            .ToList();

        result.ShouldBe(sorted);
    }

    [Fact(DisplayName = "Build Order By Throws For Invalid Property")]
    public void BuildOrderBy_Throws_ForInvalidProperty()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildOrderBy(new QueryRequest(OrderBy: [new OrderClause("Missing")])));

        ex.Message.ShouldContain("Invalid orderBy");
    }
}
