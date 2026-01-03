using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Shouldly;
using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class KyrolusEfRepositoryBaseTests
{
    [Fact(DisplayName = "BuildIncludeExpression builds nested property access")]
    public void BuildIncludeExpression_BuildsNestedPath()
    {
        var expr = KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression("Child.Value");
        expr.ShouldNotBeNull();
        var compiled = expr!.Compile();
        compiled(new Dummy { Child = new Child { Value = 10 } }).ShouldBe(10);
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues builds correct predicate")]
    public void GetPrimaryKeyFromKeyValues_BuildsPredicate()
    {
        var predicate = KyrolusEFRepositoryBase<Dummy>.GetPrimaryKeyFromKeyValues(new object?[] { 5, "k" }, new[] { "Id", "Code" });
        var func = predicate.Compile();
        func(new Dummy { Id = 5, Code = "k" }).ShouldBeTrue();
        func(new Dummy { Id = 5, Code = "x" }).ShouldBeFalse();
    }

    [Fact(DisplayName = "KyrolusIncludeGraphBuilder converts paths to graph")]
    public void IncludeGraphBuilder_ConvertsPaths()
    {
        var graph = KyrolusIncludeGraphBuilder.FromPaths<Dummy>("Child", "Child.Value");
        graph.Includes.ShouldNotBeNull();
        graph.Includes.Count.ShouldBe(2);
    }

    [Fact(DisplayName = "ConvertIncludePropertiesToExpressions returns empty on null or whitespace")]
    public void ConvertIncludePropertiesToExpressions_ReturnsEmptyOnNull()
    {
        List<string>? includeProperties = null;
        var resultNull = KyrolusEFRepositoryBase<Dummy>.ConvertIncludePropertiesToExpressions(includeProperties);
        resultNull.ShouldNotBeNull();
        resultNull.ShouldBeEmpty();

        var resultEmpty = KyrolusEFRepositoryBase<Dummy>.ConvertIncludePropertiesToExpressions([" ", ""]);
        resultEmpty.ShouldNotBeNull();
        resultEmpty.ShouldBeEmpty();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues throws when counts mismatch")]
    public void GetPrimaryKeyFromKeyValues_ThrowsOnMismatch()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Dummy>.GetPrimaryKeyFromKeyValues([1], ["Id", "Code"]));
    }

    [Fact(DisplayName = "BuildKeyPredicateFromEntity handles null values")]
    public void BuildKeyPredicateFromEntity_HandlesNullValues()
    {
        var entity = new Dummy { Id = 5, Code = null };
        var predicate = KyrolusEFRepositoryBase<Dummy>.BuildKeyPredicateFromEntity(entity, ["Id", "Code"]);
        var compiled = predicate.Compile();
        compiled(new Dummy { Id = 5, Code = null }).ShouldBeTrue();
        compiled(new Dummy { Id = 5, Code = "x" }).ShouldBeFalse();
    }

    private class Dummy
    {
        public int Id { get; set; }
        public string? Code { get; set; }
        public Child? Child { get; set; }
    }

    private class Child
    {
        public int Value { get; set; }
    }
}
