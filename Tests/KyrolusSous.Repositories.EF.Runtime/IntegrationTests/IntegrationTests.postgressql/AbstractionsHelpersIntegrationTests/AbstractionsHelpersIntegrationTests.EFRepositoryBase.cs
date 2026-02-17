using KyrolusSous.Repositories.EF.Abstractions.Helpers;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AbstractionsHelpersIntegrationTests;

public sealed class AbstractionsHelpersIntegrationTests_EFRepositoryBase(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private enum ProbeStatus
    {
        Draft = 1,
        Published = 2
    }

    private sealed class ConversionProbe
    {
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTime DueAt { get; set; }
        public TimeSpan Duration { get; set; }
        public required string Name { get; set; }
        public int Count { get; set; }
        public int? OptionalCount { get; set; }
        public ProbeStatus Status { get; set; }
    }

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

    [Fact(DisplayName = "EFRepositoryBase builds include expression for nested navigation path and compiled expression resolves loaded value")]
    public async Task BuildIncludeExpression_NestedPath_CompiledExpressionResolvesValue()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var includeExpression = KyrolusEFRepositoryBase<Product>.BuildIncludeExpression("Store.Tenant");
        includeExpression.ShouldNotBeNull();

        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Store)
            .ThenInclude(s => s!.Tenant)
            .SingleAsync(p => p.Id == DataSeeder.productLaptopId);

        var resolved = includeExpression!.Compile()(product);
        resolved.ShouldNotBeNull();
    }

    [Fact(DisplayName = "EFRepositoryBase returns null include expression for blank property path")]
    public void BuildIncludeExpression_BlankPath_ReturnsNull()
    {
        KyrolusEFRepositoryBase<Product>.BuildIncludeExpression(" ").ShouldBeNull();
    }

    [Fact(DisplayName = "EFRepositoryBase converts include properties list to expressions and skips blanks")]
    public async Task ConvertIncludePropertiesToExpressions_SkipsBlank_AndCompiledExpressionsWork()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var includeExpressions = KyrolusEFRepositoryBase<Product>.ConvertIncludePropertiesToExpressions(
            [nameof(Product.Store), " ", nameof(Product.Reviews)]);

        includeExpressions.ShouldNotBeNull();
        includeExpressions.Length.ShouldBe(2);

        var product = await db.Products
            .AsNoTracking()
            .Include(p => p.Store)
            .Include(p => p.Reviews)
            .SingleAsync(p => p.Id == DataSeeder.productLaptopId);

        var includeValues = includeExpressions.Select(expression => expression.Compile()(product)).ToList();
        includeValues.Count.ShouldBe(2);
        includeValues.All(value => value is not null).ShouldBeTrue();
    }

    [Fact(DisplayName = "EFRepositoryBase throws for invalid include path segment")]
    public void BuildIncludeExpression_InvalidPath_Throws()
    {
        Should.Throw<ArgumentException>(() => KyrolusEFRepositoryBase<Product>.BuildIncludeExpression("Store.NotFoundProperty"));
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws when key count does not match property count")]
    public void GetPrimaryKeyFromKeyValues_LengthMismatch_Throws()
    {
        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Product>.GetPrimaryKeyFromKeyValues(
                [DataSeeder.productLaptopId],
                [nameof(Product.Id), nameof(Product.Name)]));
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues converts known scalar values and finds matching probe")]
    public void GetPrimaryKeyFromKeyValues_ConvertsKnownTypes_AndMatches()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.Parse("2025-01-01T00:00:00Z", CultureInfo.InvariantCulture);
        var dueAt = DateTime.Parse("2025-12-31T00:00:00Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        var probe = new ConversionProbe
        {
            Id = id,
            CreatedAt = createdAt,
            DueAt = dueAt,
            Duration = TimeSpan.FromDays(1),
            Name = "12345",
            Count = 50,
            OptionalCount = null,
            Status = ProbeStatus.Published
        };

        var predicate = KyrolusEFRepositoryBase<ConversionProbe>.GetPrimaryKeyFromKeyValues(
            [
                id.ToString(),
                "2025-01-01T00:00:00Z",
                "2025-12-31T00:00:00Z",
                "1.00:00:00",
                12345,
                "50",
                null,
                "Published"
            ],
            [
                nameof(ConversionProbe.Id),
                nameof(ConversionProbe.CreatedAt),
                nameof(ConversionProbe.DueAt),
                nameof(ConversionProbe.Duration),
                nameof(ConversionProbe.Name),
                nameof(ConversionProbe.Count),
                nameof(ConversionProbe.OptionalCount),
                nameof(ConversionProbe.Status)
            ]);

        var matched = new[] { probe }.AsQueryable().Where(predicate).Single();
        matched.Id.ShouldBe(id);
        matched.Status.ShouldBe(ProbeStatus.Published);
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues converts enum using numeric value")]
    public void GetPrimaryKeyFromKeyValues_EnumNumericConversion_Works()
    {
        var probe = new ConversionProbe
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            DueAt = DateTime.UtcNow,
            Duration = TimeSpan.FromHours(2),
            Name = "enum-probe",
            Count = 1,
            OptionalCount = 2,
            Status = ProbeStatus.Published
        };

        var predicate = KyrolusEFRepositoryBase<ConversionProbe>.GetPrimaryKeyFromKeyValues(
            [2],
            [nameof(ConversionProbe.Status)]);

        var matched = new[] { probe }.AsQueryable().Where(predicate).Single();
        matched.Status.ShouldBe(ProbeStatus.Published);
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues throws when conversion cannot map to target type")]
    public void GetPrimaryKeyFromKeyValues_InvalidConvertibleValue_Throws()
    {
        Should.Throw<InvalidCastException>(() =>
            KyrolusEFRepositoryBase<ConversionProbe>.GetPrimaryKeyFromKeyValues(
                ["not-a-guid"],
                [nameof(ConversionProbe.Id)]));
    }

    [Fact(DisplayName = "EFRepositoryBase BuildKeyPredicateFromEntity builds a combined predicate for real entity query")]
    public async Task BuildKeyPredicateFromEntity_RealEntityQuery_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.Products.AsNoTracking().SingleAsync(p => p.Id == DataSeeder.productLaptopId);

        var predicate = KyrolusEFRepositoryBase<Product>.BuildKeyPredicateFromEntity(source, [nameof(Product.Id), nameof(Product.Name)]);
        var result = await db.Products.AsNoTracking().Where(predicate).ToListAsync();

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(DataSeeder.productLaptopId);
        result[0].Name.ShouldBe(source.Name);
    }

    [Fact(DisplayName = "EFRepositoryBase BuildKeyPredicateFromEntity throws when provided property name does not exist")]
    public void BuildKeyPredicateFromEntity_InvalidProperty_Throws()
    {
        var source = new Product
        {
            Id = Guid.NewGuid(),
            Name = "InvalidProp",
            Sku = "INV-PROP",
            StoreId = Guid.NewGuid()
        };

        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Product>.BuildKeyPredicateFromEntity(source, ["NotAProperty"]));
    }

    [Theory(DisplayName = "EFRepositoryBase maps repository read operations to cache read flags")]
    [MemberData(nameof(MapReadOperationCases))]
    public void MapReadOperation_ReturnsExpectedFlag(string caseId, string operation, KyrolusCacheReadOperations expected)
    {
        caseId.ShouldNotBeNullOrWhiteSpace();
        KyrolusEFRepositoryBase<Product>.MapReadOperation(operation).ShouldBe(expected);
    }
}
