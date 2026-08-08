using KyrolusSous.Repositories.EF.Abstractions.Helpers;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AbstractionsHelpersIntegrationTests;

public sealed class AbstractionsHelpersIntegrationTests_EFRepositoryBase(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    private sealed class NameLike
    {
        public override string ToString() => "Laptop Pro 15";
    }

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

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues converts known scalar values and matches seeded product")]
    public async Task GetPrimaryKeyFromKeyValues_ConvertsKnownTypes_AndMatchesSeededProduct()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var predicate = KyrolusEFRepositoryBase<Product>.GetPrimaryKeyFromKeyValues(
            [
                DataSeeder.productLaptopId.ToString(),
                "2024-06-01T00:00:00Z",
                "2025-12-31T00:00:00Z",
                "1.00:00:00",
                new NameLike(),
                "25",
                "10",
                null,
                "true"
            ],
            [
                nameof(Product.Id),
                nameof(Product.CreatedAt),
                nameof(Product.DiscontinuedAt),
                nameof(Product.FinishedAt),
                nameof(Product.Name),
                nameof(Product.StockQuantity),
                nameof(Product.Count),
                nameof(Product.Weight),
                nameof(Product.IsActive)
            ]);

        var matched = await db.Products.AsNoTracking().Where(predicate).SingleAsync();
        matched.Id.ShouldBe(DataSeeder.productLaptopId);
        matched.Name.ShouldBe("Laptop Pro 15");
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues converts enum using numeric value on real payment query")]
    public async Task GetPrimaryKeyFromKeyValues_EnumNumericConversion_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var predicate = KyrolusEFRepositoryBase<Payment>.GetPrimaryKeyFromKeyValues(
            [1],
            [nameof(Payment.Status)]);

        var matched = await db.Payments.AsNoTracking().SingleAsync(predicate);
        matched.Status.ShouldBe(PaymentStatus.Paid);
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues converts enum using string value on real payment query")]
    public async Task GetPrimaryKeyFromKeyValues_EnumStringConversion_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var predicate = KyrolusEFRepositoryBase<Payment>.GetPrimaryKeyFromKeyValues(
            ["Paid"],
            [nameof(Payment.Status)]);

        var matched = await db.Payments.AsNoTracking().SingleAsync(predicate);
        matched.Status.ShouldBe(PaymentStatus.Paid);
    }

    [Fact(DisplayName = "EFRepositoryBase GetPrimaryKeyFromKeyValues uses direct runtime typed values without parsing")]
    public async Task GetPrimaryKeyFromKeyValues_DirectRuntimeTypes_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.Products.AsNoTracking().SingleAsync(p => p.Id == DataSeeder.productLaptopId);

        var predicate = KyrolusEFRepositoryBase<Product>.GetPrimaryKeyFromKeyValues(
            [
                source.Id,
                source.CreatedAt,
                source.DiscontinuedAt!.Value,
                source.FinishedAt
            ],
            [
                nameof(Product.Id),
                nameof(Product.CreatedAt),
                nameof(Product.DiscontinuedAt),
                nameof(Product.FinishedAt)
            ]);

        var matched = await db.Products.AsNoTracking().Where(predicate).ToListAsync();
        matched.Count.ShouldBe(1);
        matched[0].Id.ShouldBe(DataSeeder.productLaptopId);
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
    public async Task BuildKeyPredicateFromEntity_InvalidProperty_Throws()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.Products.AsNoTracking().SingleAsync(p => p.Id == DataSeeder.productLaptopId);

        Should.Throw<ArgumentException>(() =>
            KyrolusEFRepositoryBase<Product>.BuildKeyPredicateFromEntity(source, ["NotAProperty"]));
    }

    [Fact(DisplayName = "EFRepositoryBase BuildKeyPredicateFromEntity handles nullable source values")]
    public async Task BuildKeyPredicateFromEntity_NullableSourceValue_Works()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.Products.AsNoTracking().SingleAsync(p => p.Id == DataSeeder.productBookId);

        var predicate = KyrolusEFRepositoryBase<Product>.BuildKeyPredicateFromEntity(
            source,
            [nameof(Product.Id), nameof(Product.Count)]);

        var result = await db.Products.AsNoTracking().Where(predicate).ToListAsync();
        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(DataSeeder.productBookId);
        result[0].Count.ShouldBeNull();
    }
}
