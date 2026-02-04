namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperOperatorsTests
{
    [Theory]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public void BuildFilter_Equality_Works_ForNumeric(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("IntValue", op, "5")]));

        result.Count.ShouldBe(1);
        result[0].IntValue.ShouldBe(5);
    }

    [Theory]
    [InlineData("neq")]
    [InlineData("!=")]
    [InlineData("<>")]
    public void BuildFilter_Inequality_Works_ForNumeric(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("IntValue", op, "5")]));

        result.Count.ShouldBe(2);
        result.Any(x => x.IntValue == 5).ShouldBeFalse();
    }

    [Theory]
    [InlineData("gt")]
    [InlineData(">")]
    public void BuildFilter_GreaterThan_Works_ForDateOnly(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("DateOnlyValue", op, "2024-06-15")]));

        result.Count.ShouldBe(2);
        result.All(x => x.DateOnlyValue > new DateOnly(2024, 6, 15)).ShouldBeTrue();
    }

    [Theory]
    [InlineData("lte")]
    [InlineData("<=")]
    public void BuildFilter_LessThanOrEqual_Works_ForDecimal(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("DecimalValue", op, "25")]));

        result.Count.ShouldBe(2);
        result.All(x => x.DecimalValue <= 25m).ShouldBeTrue();
    }

    [Theory]
    [InlineData("contains")]
    public void BuildFilter_StringContains_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "am")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Gamma");
    }

    [Theory]
    [InlineData("startswith")]
    public void BuildFilter_StringStartsWith_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "Al")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alpha");
    }

    [Theory]
    [InlineData("endswith")]
    public void BuildFilter_StringEndsWith_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "ta")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Beta");
    }

    [Theory]
    [InlineData("eq")]
    [InlineData("=")]
    [InlineData("==")]
    public void BuildFilter_Equality_Works_ForEnum(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Status", op, "Active")]));

        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(RuntimeQueryHelperTestData.TestStatus.Active);
    }

    [Theory]
    [InlineData("eq")]
    [InlineData("=")]
    public void BuildFilter_Equality_Works_ForBool(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("IsActive", op, "false")]));

        result.Count.ShouldBe(1);
        result[0].IsActive.ShouldBeFalse();
    }
}
