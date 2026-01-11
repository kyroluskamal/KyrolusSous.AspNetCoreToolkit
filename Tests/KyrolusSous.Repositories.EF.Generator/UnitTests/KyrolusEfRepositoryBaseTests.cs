using System.Reflection;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Shouldly;

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
        var predicate = KyrolusEFRepositoryBase<Dummy>.GetPrimaryKeyFromKeyValues([5, "k"], ["Id", "Code"]);
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
    // -------------------------
    // BuildIncludeExpression
    // -------------------------

    [Fact(DisplayName = "BuildIncludeExpression returns null on null/empty/whitespace")]
    public void BuildIncludeExpression_ReturnsNull_OnEmpty()
    {
        KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression(null!).ShouldBeNull();
        KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression("").ShouldBeNull();
        KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression("   ").ShouldBeNull();
    }

    [Fact(DisplayName = "BuildIncludeExpression throws on invalid property path")]
    public void BuildIncludeExpression_Throws_OnInvalidPath()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression("DoesNotExist"));

        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Dummy>.BuildIncludeExpression("Child.DoesNotExist"));
    }

    [Fact(DisplayName = "BuildIncludeExpression can access fields (PropertyOrField)")]
    public void BuildIncludeExpression_AccessesFields()
    {
        var expr = KyrolusEFRepositoryBase<DummyWithField>.BuildIncludeExpression("Child.Value");
        expr.ShouldNotBeNull();

        var compiled = expr!.Compile();
        compiled(new DummyWithField { Child = new ChildField { Value = 77 } }).ShouldBe(77);
    }

    // -------------------------
    // ConvertIncludePropertiesToExpressions
    // -------------------------

    [Fact(DisplayName = "ConvertIncludePropertiesToExpressions filters null/whitespace and keeps valid")]
    public void ConvertIncludePropertiesToExpressions_FiltersAndKeepsValid()
    {
        var list = new List<string?> { null, " ", "", "Child.Value", "Id" }
            .Cast<string>() // runtime may still contain nulls; we want to pass them through
            .ToList();

        var result = KyrolusEFRepositoryBase<Dummy>.ConvertIncludePropertiesToExpressions(list);

        result.ShouldNotBeNull();
        result.Length.ShouldBe(2);
    }

    // -------------------------
    // GetPrimaryKeyFromKeyValues - Conversions & branches
    // -------------------------

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts string->int via Convert.ChangeType")]
    public void GetPrimaryKeyFromKeyValues_Converts_StringToInt()
    {
        var pred = KyrolusEFRepositoryBase<DummyIntKey>.GetPrimaryKeyFromKeyValues(
            ["5"],
            [nameof(DummyIntKey.Id)]);

        var func = pred.Compile();
        func(new DummyIntKey { Id = 5 }).ShouldBeTrue();
        func(new DummyIntKey { Id = 6 }).ShouldBeFalse();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts value->string using ToString")]
    public void GetPrimaryKeyFromKeyValues_Converts_ToString()
    {
        var pred = KyrolusEFRepositoryBase<DummyStringKey>.GetPrimaryKeyFromKeyValues(
            [123],
            [nameof(DummyStringKey.Code)]);

        var func = pred.Compile();
        func(new DummyStringKey { Code = "123" }).ShouldBeTrue();
        func(new DummyStringKey { Code = "124" }).ShouldBeFalse();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts string->Guid")]
    public void GetPrimaryKeyFromKeyValues_Converts_StringToGuid()
    {
        var g = Guid.NewGuid();

        var pred = KyrolusEFRepositoryBase<DummyGuidKey>.GetPrimaryKeyFromKeyValues(
            [g.ToString()],
            [nameof(DummyGuidKey.Id)]);

        var func = pred.Compile();
        func(new DummyGuidKey { Id = g }).ShouldBeTrue();
        func(new DummyGuidKey { Id = Guid.NewGuid() }).ShouldBeFalse();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues supports Guid instance (IsInstanceOfType path)")]
    public void GetPrimaryKeyFromKeyValues_GuidInstance_NoConversionNeeded()
    {
        var g = Guid.NewGuid();

        var pred = KyrolusEFRepositoryBase<DummyGuidKey>.GetPrimaryKeyFromKeyValues(
            [g],
            [nameof(DummyGuidKey.Id)]);

        var func = pred.Compile();
        func(new DummyGuidKey { Id = g }).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts string->DateTimeOffset (RoundtripKind)")]
    public void GetPrimaryKeyFromKeyValues_Converts_StringToDateTimeOffset()
    {
        // roundtrip string
        var dto = new DateTimeOffset(2026, 1, 10, 12, 30, 0, TimeSpan.FromHours(1));
        var s = dto.ToString("O");

        var pred = KyrolusEFRepositoryBase<DummyDateTimeOffsetKey>.GetPrimaryKeyFromKeyValues(
            [s],
            [nameof(DummyDateTimeOffsetKey.Id)]);

        var func = pred.Compile();
        func(new DummyDateTimeOffsetKey { Id = dto }).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts string->DateTime (RoundtripKind)")]
    public void GetPrimaryKeyFromKeyValues_Converts_StringToDateTime()
    {
        var dt = new DateTime(2026, 1, 10, 11, 22, 33, DateTimeKind.Utc);
        var s = dt.ToString("O");

        var pred = KyrolusEFRepositoryBase<DummyDateTimeKey>.GetPrimaryKeyFromKeyValues(
            [s],
            [nameof(DummyDateTimeKey.Id)]);

        var func = pred.Compile();
        func(new DummyDateTimeKey { Id = dt }).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues converts string->TimeSpan")]
    public void GetPrimaryKeyFromKeyValues_Converts_StringToTimeSpan()
    {
        var ts = TimeSpan.FromMinutes(90);
        var s = ts.ToString();

        var pred = KyrolusEFRepositoryBase<DummyTimeSpanKey>.GetPrimaryKeyFromKeyValues(
            [s],
            [nameof(DummyTimeSpanKey.Id)]);

        var func = pred.Compile();
        func(new DummyTimeSpanKey { Id = ts }).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues parses enum from string (ignoreCase)")]
    public void GetPrimaryKeyFromKeyValues_ParsesEnum_FromString()
    {
        var pred = KyrolusEFRepositoryBase<DummyEnumKey>.GetPrimaryKeyFromKeyValues(
            ["two"],
            [nameof(DummyEnumKey.Status)]);

        var func = pred.Compile();
        func(new DummyEnumKey { Status = MyStatus.Two }).ShouldBeTrue();
        func(new DummyEnumKey { Status = MyStatus.One }).ShouldBeFalse();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues creates enum from numeric value")]
    public void GetPrimaryKeyFromKeyValues_Enum_FromNumber()
    {
        var pred = KyrolusEFRepositoryBase<DummyEnumKey>.GetPrimaryKeyFromKeyValues(
            [2],
            [nameof(DummyEnumKey.Status)]);

        var func = pred.Compile();
        func(new DummyEnumKey { Status = MyStatus.Two }).ShouldBeTrue();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues throws when enum string is invalid (Enum.Parse throws)")]
    public void GetPrimaryKeyFromKeyValues_Enum_InvalidString_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<DummyEnumKey>.GetPrimaryKeyFromKeyValues(
                ["not-a-status"],
                [nameof(DummyEnumKey.Status)]));
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues supports nullable key when null provided")]
    public void GetPrimaryKeyFromKeyValues_Nullable_AllowsNull()
    {
        var pred = KyrolusEFRepositoryBase<DummyNullableIntKey>.GetPrimaryKeyFromKeyValues(
            [null],
            [nameof(DummyNullableIntKey.Id)]);

        var func = pred.Compile();
        func(new DummyNullableIntKey { Id = null }).ShouldBeTrue();
        func(new DummyNullableIntKey { Id = 1 }).ShouldBeFalse();
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues throws when null provided for non-nullable value type")]
    public void GetPrimaryKeyFromKeyValues_NonNullable_Null_Throws()
    {
        // Expression.Constant(null, typeof(int)) should throw
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<DummyIntKey>.GetPrimaryKeyFromKeyValues(
                [null],
                [nameof(DummyIntKey.Id)]));
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues throws when Convert.ChangeType fails (e.g. 'abc' -> int)")]
    public void GetPrimaryKeyFromKeyValues_Convertible_Invalid_Throws()
    {
        Should.Throw<FormatException>(() =>
        {
            // Convert.ChangeType happens while building expression (inside GetPrimaryKeyFromKeyValues)
            KyrolusEFRepositoryBase<DummyIntKey>.GetPrimaryKeyFromKeyValues(
                ["abc"],
                [nameof(DummyIntKey.Id)]);
        });
    }

    [Fact(DisplayName = "GetPrimaryKeyFromKeyValues hits non-IConvertible fallback then fails building constant")]
    public void GetPrimaryKeyFromKeyValues_NotConvertible_Fallback_Path()
    {
        // This forces: KnownTypes false, Enum false, Convertible false => returns value as-is
        // then Expression.Constant(value, int) throws because value isn't assignable to int.
        Should.Throw<ArgumentException>(() =>
        {
            KyrolusEFRepositoryBase<DummyIntKey>.GetPrimaryKeyFromKeyValues(
                [new NotConvertible()],
                [nameof(DummyIntKey.Id)]);
        });
    }

    // -------------------------
    // BuildKeyPredicateFromEntity - extra throws branch
    // -------------------------

    [Fact(DisplayName = "BuildKeyPredicateFromEntity throws when key property does not exist")]
    public void BuildKeyPredicateFromEntity_Throws_WhenPropMissing()
    {
        var entity = new DummyIntKey { Id = 5 };

        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<DummyIntKey>.BuildKeyPredicateFromEntity(entity, ["DoesNotExist"]));
    }
    private static Type RepoType => typeof(KyrolusEFRepositoryBase<>).MakeGenericType(typeof(Dummy));

    private static object? Invoke(string methodName, params object?[] args)
    {
        var m = RepoType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
        m.ShouldNotBeNull($"Method '{methodName}' was not found.");
        return m!.Invoke(null, args);
    }

    [Fact(DisplayName = "ConvertToType returns null when value is null")]
    public void ConvertToType_Null_ReturnsNull()
    {
        var result = Invoke("ConvertToType", null, typeof(int?));
        result.ShouldBeNull();
    }

    [Fact(DisplayName = "TryConvertGuid covers Guid instance branch")]
    public void TryConvertGuid_GuidInstance_Branch()
    {
        var g = Guid.NewGuid();
        object?[] args = [g, null]; // out param placeholder
        var ok = (bool)Invoke("TryConvertGuid", args)!;

        ok.ShouldBeTrue();
        args[1].ShouldBe(g);
    }

    [Fact(DisplayName = "TryConvertDateTimeOffset covers DateTimeOffset instance branch")]
    public void TryConvertDateTimeOffset_Instance_Branch()
    {
        var dto = new DateTimeOffset(2026, 1, 10, 10, 0, 0, TimeSpan.FromHours(1));
        object?[] args = [dto, null];
        var ok = (bool)Invoke("TryConvertDateTimeOffset", args)!;

        ok.ShouldBeTrue();
        args[1].ShouldBe(dto);
    }

    [Fact(DisplayName = "TryConvertDateTime covers DateTime instance branch")]
    public void TryConvertDateTime_Instance_Branch()
    {
        var dt = new DateTime(2026, 1, 10, 10, 0, 0, DateTimeKind.Utc);
        object?[] args = [dt, null];
        var ok = (bool)Invoke("TryConvertDateTime", args)!;

        ok.ShouldBeTrue();
        args[1].ShouldBe(dt);
    }

    [Fact(DisplayName = "TryConvertTimeSpan covers TimeSpan instance branch")]
    public void TryConvertTimeSpan_Instance_Branch()
    {
        var ts = TimeSpan.FromMinutes(90);
        object?[] args = [ts, null];
        var ok = (bool)Invoke("TryConvertTimeSpan", args)!;

        ok.ShouldBeTrue();
        args[1].ShouldBe(ts);
    }

    // -------------------------
    // Dummies
    // -------------------------

    private class DummyWithField
    {
        public ChildField? Child; // field (not property)
    }

    private class ChildField
    {
        public int Value { get; set; }
    }

    private class DummyIntKey
    {
        public int Id { get; set; }
    }

    private class DummyNullableIntKey
    {
        public int? Id { get; set; }
    }

    private class DummyStringKey
    {
        public string? Code { get; set; }
    }

    private class DummyGuidKey
    {
        public Guid Id { get; set; }
    }

    private class DummyDateTimeOffsetKey
    {
        public DateTimeOffset Id { get; set; }
    }

    private class DummyDateTimeKey
    {
        public DateTime Id { get; set; }
    }

    private class DummyTimeSpanKey
    {
        public TimeSpan Id { get; set; }
    }

    private class DummyEnumKey
    {
        public MyStatus Status { get; set; }
    }

    private enum MyStatus
    {
        One = 1,
        Two = 2
    }

    private sealed class NotConvertible
    {
        public override string ToString() => "NotConvertible";
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
