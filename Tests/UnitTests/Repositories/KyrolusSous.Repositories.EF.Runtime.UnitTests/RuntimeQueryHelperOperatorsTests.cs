namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperOperatorsTests
{
    [Theory(DisplayName = "Build Filter Equality Works For Numeric")]
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

    [Theory(DisplayName = "Build Filter Inequality Works For Numeric")]
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

    [Theory(DisplayName = "Build Filter Greater Than Works For Date Only")]
    [InlineData("gt")]
    [InlineData(">")]
    public void BuildFilter_GreaterThan_Works_ForDateOnly(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("DateOnlyValue", op, "2024-06-15")]));

        result.Count.ShouldBe(2);
        result.All(x => x.DateOnlyValue > new DateOnly(2024, 6, 15)).ShouldBeTrue();
    }

    [Theory(DisplayName = "Build Filter Less Than Or Equal Works For Decimal")]
    [InlineData("lte")]
    [InlineData("<=")]
    public void BuildFilter_LessThanOrEqual_Works_ForDecimal(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("DecimalValue", op, "25")]));

        result.Count.ShouldBe(2);
        result.All(x => x.DecimalValue <= 25m).ShouldBeTrue();
    }

    [Theory(DisplayName = "Build Filter String Contains Works")]
    [InlineData("contains")]
    public void BuildFilter_StringContains_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "am")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Gamma");
    }

    [Theory(DisplayName = "Build Filter String Starts With Works")]
    [InlineData("startswith")]
    public void BuildFilter_StringStartsWith_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "Al")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Alpha");
    }

    [Theory(DisplayName = "Build Filter String Ends With Works")]
    [InlineData("endswith")]
    public void BuildFilter_StringEndsWith_Works(string op)
    {
        var result = RuntimeQueryHelperTestData.ApplyFilter(
            new QueryRequest(Filters: [new FilterClause("Name", op, "ta")]));

        result.Count.ShouldBe(1);
        result[0].Name.ShouldBe("Beta");
    }

    [Theory(DisplayName = "Build Filter Equality Works For Enum")]
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

    [Theory(DisplayName = "Build Filter Equality Works For Bool")]
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
