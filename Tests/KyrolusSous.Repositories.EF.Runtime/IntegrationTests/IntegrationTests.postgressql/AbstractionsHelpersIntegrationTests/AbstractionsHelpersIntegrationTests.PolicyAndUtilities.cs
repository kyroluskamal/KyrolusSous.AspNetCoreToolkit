using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using System.Linq.Expressions;

namespace KyrolusSous.Repositories.EF.Runtime.IntegrationTests.postgressql.AbstractionsHelpersIntegrationTests;

public sealed class AbstractionsHelpersIntegrationTests_PolicyAndUtilities(WebApplicationFactory<Program> factory) : KyrolusRuntimePSFixture(factory)
{
    [Fact(DisplayName = "Repository policy extensions compose multiple global filters and apply them to real queryable")]
    public async Task PolicyExtensions_GlobalFilters_ComposeAndApply()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var policy = new KyrolusRepositoryPolicy()
            .AddGlobalWhereFilter<Product>(p => p.Price > 100m)
            .AddGlobalWhereFilter<Product>(p => p.StockQuantity < 30);

        var filter = policy.GetGlobalQueryFilter<Product>();
        filter.ShouldNotBeNull();

        var filtered = await filter!(db.Products.AsNoTracking()).ToListAsync();
        filtered.Count.ShouldBe(1);
        filtered.Single().Name.ShouldBe("Laptop Pro 15");
    }

    [Fact(DisplayName = "Repository policy extensions manage cache policies and cache read operations")]
    public void PolicyExtensions_CachePolicies_AndReadOperations_Work()
    {
        var defaultPolicy = new KyrolusCachePolicy(Enabled: false, KeySuffix: "default");
        var entityPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "entity");
        var policy = new KyrolusRepositoryPolicy
        {
            DefaultCachePolicy = defaultPolicy,
            DefaultCacheReadOperations = KyrolusCacheReadOperations.GetByIdAsync
        };

        policy.SetCachePolicy<Product>(entityPolicy);
        policy.SetCacheReadOperations<Product>(KyrolusCacheReadOperations.GetAllAsync);

        policy.GetCachePolicy<Product>().ShouldBe(entityPolicy);
        policy.GetCachePolicy<Category>().ShouldBe(defaultPolicy);
        policy.GetCacheReadOperations<Product>().ShouldBe(KyrolusCacheReadOperations.GetAllAsync);
        policy.GetCacheReadOperations<Category>().ShouldBe(KyrolusCacheReadOperations.GetByIdAsync);
    }

    [Fact(DisplayName = "Repository policy extensions normalize include properties and remove defaults when null")]
    public void PolicyExtensions_DefaultIncludeProperties_NormalizeAndRemove()
    {
        var policy = new KyrolusRepositoryPolicy();

        policy.SetDefaultIncludeProperties<Product>(" Store ", "Reviews", "Store", "", "   ");
        policy.GetDefaultIncludeProperties<Product>().ShouldBe(["Store", "Reviews"]);

        policy.SetDefaultIncludeProperties<Product>(null!);
        policy.GetDefaultIncludeProperties<Product>().ShouldBeEmpty();
    }

    [Fact(DisplayName = "Expression fingerprint evaluates captured closure values to constants")]
    public void ExpressionFingerprint_ClosureValues_AreInFingerprint()
    {
        var threshold = 25;
        var holder = new { Prefix = "Laptop" };
        Expression<Func<Product, bool>> expression = product => product.StockQuantity > threshold && product.Name.StartsWith(holder.Prefix);

        var fingerprint = KyrolusExpressionFingerprint.Build(expression);

        fingerprint.ShouldContain("25");
        fingerprint.ShouldContain("Laptop");
        fingerprint.ShouldNotContain("threshold");
    }

    [Fact(DisplayName = "Expression fingerprint throws for null expression")]
    public void ExpressionFingerprint_NullExpression_Throws()
    {
        Should.Throw<ArgumentNullException>(() => KyrolusExpressionFingerprint.Build(null!));
    }

    [Fact(DisplayName = "QueryRequest TryParse handles blank valid and invalid payloads")]
    public void QueryRequest_TryParse_HandlesExpectedScenarios()
    {
        QueryRequest.TryParse(" ", null, out var blank).ShouldBeTrue();
        blank.ShouldNotBeNull();
        blank.Filters.ShouldBeNull();

        var json = "{\"filters\":[{\"property\":\"Name\",\"operator\":\"eq\",\"value\":\"Clean Code\"}],\"asNoTracking\":true}";
        var encoded = WebUtility.UrlEncode(json);
        QueryRequest.TryParse(encoded, null, out var parsed).ShouldBeTrue();
        parsed.ShouldNotBeNull();
        parsed.AsNoTracking.ShouldBe(true);
        parsed.Filters.ShouldNotBeNull();
        parsed.Filters!.Length.ShouldBe(1);
        parsed.Filters[0].Property.ShouldBe("Name");
        parsed.Filters[0].Operator.ShouldBe("eq");
        parsed.Filters[0].Value.ShouldBe("Clean Code");

        QueryRequest.TryParse("{invalid-json", null, out var invalid).ShouldBeFalse();
        invalid.ShouldNotBeNull();
    }

    [Fact(DisplayName = "QueryRequest Parse throws for invalid payload")]
    public void QueryRequest_Parse_Invalid_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => QueryRequest.Parse("{bad-json", null));
    }

    [Fact(DisplayName = "Noop repository policy provider always returns null policy")]
    public async Task NoopRepositoryPolicyProvider_ReturnsNullPolicy()
    {
        var context = new KyrolusRepositoryPolicyContext(typeof(Product), nameof(Product), typeof(ApplicationDbContext), "scope", "tenant-a");
        var policy = await KyrolusNoopRepositoryPolicyProvider.Instance.GetPolicyAsync(context);
        policy.ShouldBeNull();
    }

    [Fact(DisplayName = "Concurrency helper BuildConcurrencyInfo throws for null exception and returns null for empty entries")]
    public async Task ConcurrencyHelper_BuildInfo_NullAndEmptyEntries_Handled()
    {
        await Should.ThrowAsync<ArgumentNullException>(() => ConcurrencyHelper.BuildConcurrencyInfoAsync(null!));

        var ex = new DbUpdateConcurrencyException("no entries");
        var info = await ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionPropertyName: "RowVersion");
        info.ShouldBeNull();
    }

    [Fact(DisplayName = "Concurrency helper returns success when operation succeeds on first attempt")]
    public async Task ConcurrencyHelper_Execute_SuccessFirstAttempt()
    {
        var attempts = 0;
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 2 };

        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            action: () =>
            {
                attempts++;
                return Task.FromResult("ok");
            },
            policy: policy);

        attempts.ShouldBe(1);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        result.Value.ShouldBe("ok");
        result.Exception.ShouldBeNull();
    }

    [Fact(DisplayName = "Concurrency helper retries on concurrency exception then succeeds")]
    public async Task ConcurrencyHelper_Execute_RetryThenSuccess()
    {
        var attempts = 0;
        var policy = new KyrolusRepositoryPolicy
        {
            ConcurrencyRetryCount = 2,
            ConcurrencyRetryDelay = TimeSpan.FromMilliseconds(1)
        };

        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            action: () =>
            {
                attempts++;
                if (attempts == 1)
                    throw new DbUpdateConcurrencyException("first");
                return Task.FromResult(42);
            },
            policy: policy);

        attempts.ShouldBe(2);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        result.Value.ShouldBe(42);
    }

    [Fact(DisplayName = "Concurrency helper returns conflict at retry limit and uses factory concurrency info")]
    public async Task ConcurrencyHelper_Execute_ConflictAtLimit_UsesFactoryInfo()
    {
        var attempts = 0;
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1 };

        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync<string>(
            action: () =>
            {
                attempts++;
                throw new DbUpdateConcurrencyException("always conflict");
            },
            policy: policy,
            concurrencyInfoFactory: _ => Task.FromResult<ConcurrencyInfo?>(new ConcurrencyInfo(
                originalRowVersion: [(byte)1, (byte)2, (byte)3],
                currentRowVersion: [(byte)4, (byte)5, (byte)6],
                databaseValues: new Dictionary<string, object?> { ["Name"] = "db" })));

        attempts.ShouldBe(2);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
        result.Exception.ShouldNotBeNull();
        result.Concurrency.ShouldNotBeNull();
        result.Concurrency!.Value.OriginalRowVersion.ShouldBe([(byte)1, (byte)2, (byte)3]);
        result.Concurrency!.Value.CurrentRowVersion.ShouldBe([(byte)4, (byte)5, (byte)6]);
        result.Concurrency!.Value.RetryCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Concurrency helper returns failed result for non-concurrency exceptions")]
    public async Task ConcurrencyHelper_Execute_NonConcurrencyFailure_ReturnsFailed()
    {
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 2 };

        var result = await ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync<int>(
            action: () => throw new InvalidOperationException("boom"),
            policy: policy);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Failed);
        result.Exception.ShouldBeOfType<InvalidOperationException>();
    }
}
