namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public class RuntimeQueryHelperErrorTests
{
    [Fact]
    public void BuildFilter_ReturnsNull_WhenNoFilters()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        helper.BuildFilter(new QueryRequest()).ShouldBeNull();
        helper.BuildFilter(new QueryRequest(Filters: [])).ShouldBeNull();
    }

    [Fact]
    public void BuildFilter_Throws_ForMissingProperty()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("", "eq", "1")])));

        ex.Message.ShouldContain("Property");
    }

    [Fact]
    public void BuildFilter_Throws_ForMissingOperator()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "", "1")])));

        ex.Message.ShouldContain("Operator");
    }

    [Fact]
    public void BuildFilter_Throws_ForInvalidPropertyPath()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Missing", "eq", "1")])));

        ex.Message.ShouldContain("Invalid filter");
    }

    [Fact]
    public void BuildFilter_Throws_ForNullOperatorOnNonNullable()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "isnull", null)])));

        ex.Message.ShouldContain("supported only for nullable");
    }

    [Fact]
    public void BuildFilter_Throws_ForInvalidInWithNullOnNonNullable()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("IntValue", "in", "null,5")])));

        ex.Message.ShouldContain("in");
    }

    [Fact]
    public void BuildFilter_Throws_ForBetweenOnUnsupportedType()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Name", "between", "A..Z")])));

        ex.Message.ShouldContain("Invalid filter");
    }

    [Fact]
    public void BuildFilter_Throws_ForUnsupportedOperatorOnEnum()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Status", "gt", "Active")])));

        ex.Message.ShouldContain("Unsupported operator");
    }

    [Fact]
    public void BuildFilter_Throws_ForAnyOnNonCollection()
    {
        var helper = new RuntimeQueryHelper<RuntimeQueryHelperTestData.TestEntity>();
        var ex = Should.Throw<ArgumentException>(() =>
            helper.BuildFilter(new QueryRequest(Filters: [new FilterClause("Name", "any", "A")])));

        ex.Message.ShouldContain("Invalid filter");
    }
}
