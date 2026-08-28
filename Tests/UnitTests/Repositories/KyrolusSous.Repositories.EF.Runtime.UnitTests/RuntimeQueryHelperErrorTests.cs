namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperErrorTests
{
    [Fact(DisplayName = "Build Filter Returns Null When No Filters")]
    public void BuildFilter_ReturnsNull_WhenNoFilters()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        helper.BuildFilter(new QueryRequest()).ShouldBeNull();
        helper.BuildFilter(new QueryRequest(Filters: [])).ShouldBeNull();
    }

    [Fact(DisplayName = "Build Filter Throws For Missing Property")]
    public void BuildFilter_Throws_ForMissingProperty()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("", "eq", "1")])));

        ex.Message.ShouldContain("Property");
    }

    [Fact(DisplayName = "Build Filter Throws For Missing Operator")]
    public void BuildFilter_Throws_ForMissingOperator()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "", "1")])));

        ex.Message.ShouldContain("Operator");
    }

    [Fact(DisplayName = "Build Filter Throws For Invalid Property Path")]
    public void BuildFilter_Throws_ForInvalidPropertyPath()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Missing", "eq", "1")])));

        ex.Message.ShouldContain("Invalid filter");
    }

    [Fact(DisplayName = "Build Filter Throws For Null Operator On Non Nullable")]
    public void BuildFilter_Throws_ForNullOperatorOnNonNullable()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "isnull", null)])));

        ex.Message.ShouldContain("supported only for nullable");
    }

    [Fact(DisplayName = "Build Filter Throws For Invalid In With Null On Non Nullable")]
    public void BuildFilter_Throws_ForInvalidInWithNullOnNonNullable()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "in", "null,5")])));

        ex.Message.ShouldContain("in");
    }

    [Fact(DisplayName = "Build Filter Throws For Between On Unsupported Type")]
    public void BuildFilter_Throws_ForBetweenOnUnsupportedType()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Name", "between", "A..Z")])));

        ex.Message.ShouldContain("Invalid filter");
    }

    [Fact(DisplayName = "Build Filter Throws For Unsupported Operator On Enum")]
    public void BuildFilter_Throws_ForUnsupportedOperatorOnEnum()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Status", "gt", "Active")])));

        ex.Message.ShouldContain("Unsupported operator");
    }

    [Fact(DisplayName = "Build Filter Throws For Any On Non Collection")]
    public void BuildFilter_Throws_ForAnyOnNonCollection()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Name", "any", "A")])));

        ex.Message.ShouldContain("Invalid filter");
    }
}
