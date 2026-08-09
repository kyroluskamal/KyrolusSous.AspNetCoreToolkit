using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Policy;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class KyrolusEFRepositoryBaseUnitTests
{
    public static TheoryData<string, object?[], string[]> InvalidKnownTypeConversionCases => new()
    {
        { "datetimeoffset-invalid-string", ["not-a-dto"], [nameof(PrimaryKeyEntity.CreatedAt)] },
        { "datetime-invalid-string", ["not-a-date"], [nameof(PrimaryKeyEntity.DiscontinuedAt)] },
        { "timespan-invalid-string", ["not-a-timespan"], [nameof(PrimaryKeyEntity.FinishedAt)] }
    };

    public static TheoryData<string, string, KyrolusCacheReadOperations> MapReadOperationCases => new()
    {
        { "get-by-id", "GetByIdAsync", KyrolusCacheReadOperations.GetByIdAsync },
        { "get-all", "GetAllAsync", KyrolusCacheReadOperations.GetAllAsync },
        { "get-by-id-compiled", "GetByIdCompiledAsync", KyrolusCacheReadOperations.GetByIdCompiledAsync },
        { "get-all-compiled", "GetAllCompiledAsync", KyrolusCacheReadOperations.GetAllCompiledAsync },
        { "get-all-including-deleted", "GetAllIncludingDeletedAsync", KyrolusCacheReadOperations.GetAllIncludingDeletedAsync },
        { "get-deleted-only", "GetDeletedOnlyAsync", KyrolusCacheReadOperations.GetDeletedOnlyAsync },
        { "get-by-id-including-deleted", "GetByIdIncludingDeletedAsync", KyrolusCacheReadOperations.GetByIdIncludingDeletedAsync },
        { "unknown", "OtherOperation", KyrolusCacheReadOperations.None }
    };

    [Fact(DisplayName = "EFRepositoryBase returns null include expression for blank property path")]
    public void BuildIncludeExpression_BlankPath_ReturnsNull()
    {
        KyrolusEFRepositoryBase<PrimaryKeyEntity>.BuildIncludeExpression(" ").ShouldBeNull();
    }

    [Fact(DisplayName = "EFRepositoryBase converts null include list to empty expression array")]
    public void ConvertIncludePropertiesToExpressions_NullList_ReturnsEmptyArray()
    {
        var includeExpressions = KyrolusEFRepositoryBase<PrimaryKeyEntity>.ConvertIncludePropertiesToExpressions(null);
        includeExpressions.ShouldNotBeNull();
        includeExpressions.ShouldBeEmpty();
    }

    [Fact(DisplayName = "EFRepositoryBase throws for invalid include path segment")]
    public void BuildIncludeExpression_InvalidPath_Throws()
    {
        Should.Throw<ArgumentException>(() => KyrolusEFRepositoryBase<PrimaryKeyEntity>.BuildIncludeExpression("Missing.Path"));
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws when key count does not match property count")]
    public void GetPrimaryKeyFromKeyValues_LengthMismatch_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<PrimaryKeyEntity>.GetPrimaryKeyFromKeyValues(
                [Guid.NewGuid()],
                [nameof(PrimaryKeyEntity.Id), nameof(PrimaryKeyEntity.Name)]));
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws when conversion cannot map to target type")]
    public void GetPrimaryKeyFromKeyValues_InvalidConvertibleValue_Throws()
    {
        Should.Throw<FormatException>(() =>
            KyrolusEFRepositoryBase<PrimaryKeyEntity>.GetPrimaryKeyFromKeyValues(
                ["not-a-guid"],
                [nameof(PrimaryKeyEntity.Id)]));
    }

    [Theory(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws for invalid date and time literals")]
    [MemberData(nameof(InvalidKnownTypeConversionCases))]
    public void GetPrimaryKeyFromKeyValues_InvalidKnownTypeLiterals_Throw(string caseId, object?[] keyValues, string[] keyProperties)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();

        Should.Throw<Exception>(() =>
            KyrolusEFRepositoryBase<PrimaryKeyEntity>.GetPrimaryKeyFromKeyValues(
                keyValues,
                keyProperties));
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws when value is non-convertible for unknown target type")]
    public void GetPrimaryKeyFromKeyValues_NonConvertibleUnknownType_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<PrimaryKeyEntity>.GetPrimaryKeyFromKeyValues(
                [new NameLike()],
                [nameof(PrimaryKeyEntity.RowVersion)]));
    }

    [Theory(DisplayName = "EFRepositoryBase maps repository read operations to cache read flags")]
    [MemberData(nameof(MapReadOperationCases))]
    public void MapReadOperation_ReturnsExpectedFlag(string caseId, string operation, KyrolusCacheReadOperations expected)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        KyrolusEFRepositoryBase<PrimaryKeyEntity>.MapReadOperation(operation).ShouldBe(expected);
    }

    private sealed class PrimaryKeyEntity
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public DateTime? DiscontinuedAt { get; init; }
        public TimeSpan FinishedAt { get; init; }
        public byte[] RowVersion { get; init; } = [];
    }

    private sealed class NameLike
    {
        public override string ToString() => "NameLike";
    }
}
