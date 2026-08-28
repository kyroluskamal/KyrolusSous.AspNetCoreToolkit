using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Elastic.Clients.Elasticsearch;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Elasticsearch.UnitTests;

[KyrolusElasticIndex("products", NumberOfShards = 3, NumberOfReplicas = 2, UseAlias = true, Alias = "products-live")]
[KyrolusSyncToElasticsearch(IndexName = "products", IdProperty = "Id")]
public class TestProductDocument
{
    public string Id { get; set; } = string.Empty;

    [KyrolusElasticText(Analyzer = "arabic")]
    public string Title { get; set; } = string.Empty;

    [KyrolusElasticText]
    public string Description { get; set; } = string.Empty;

    [KyrolusElasticKeyword]
    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsFeatured { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    [KyrolusElasticGeoPoint]
    public KyrolusGeoCoordinate? Location { get; set; }

    [KyrolusElasticDenseVector(1536)]
    public float[]? Embedding { get; set; }
}

public class TestTenantProvider : IKyrolusTenantProvider
{
    public string? CurrentTenantId => "tenant_alpha";
}

public class ElasticsearchUnitTests
{
    [Fact]
    public void ElasticIndexAttribute_ReadsPropertiesCorrectly()
    {
        var attr = typeof(TestProductDocument).GetCustomAttributes(typeof(KyrolusElasticIndexAttribute), false)
            .Cast<KyrolusElasticIndexAttribute>()
            .FirstOrDefault();

        attr.ShouldNotBeNull();
        attr.IndexName.ShouldBe("products");
        attr.NumberOfShards.ShouldBe(3);
        attr.NumberOfReplicas.ShouldBe(2);
        attr.UseAlias.ShouldBeTrue();
        attr.Alias.ShouldBe("products-live");
    }

    [Fact]
    public void ElasticMappingAttributes_ReadMetadataCorrectly()
    {
        var titleAttr = typeof(TestProductDocument).GetProperty("Title")?
            .GetCustomAttributes(typeof(KyrolusElasticTextAttribute), false)
            .Cast<KyrolusElasticTextAttribute>()
            .FirstOrDefault();

        titleAttr.ShouldNotBeNull();
        titleAttr.Analyzer.ShouldBe("arabic");

        var catAttr = typeof(TestProductDocument).GetProperty("Category")?
            .GetCustomAttributes(typeof(KyrolusElasticKeywordAttribute), false)
            .Cast<KyrolusElasticKeywordAttribute>()
            .FirstOrDefault();

        catAttr.ShouldNotBeNull();
        catAttr.Index.ShouldBeTrue();

        var vectorAttr = typeof(TestProductDocument).GetProperty("Embedding")?
            .GetCustomAttributes(typeof(KyrolusElasticDenseVectorAttribute), false)
            .Cast<KyrolusElasticDenseVectorAttribute>()
            .FirstOrDefault();

        vectorAttr.ShouldNotBeNull();
        vectorAttr.Dimensions.ShouldBe(1536);
    }

    [Fact]
    public void KyrolusElasticsearchOptions_Defaults_AreValid()
    {
        var options = new KyrolusElasticsearchOptions();

        options.Url.ShouldBe("http://localhost:9200");
        options.AutoCreateIndices.ShouldBeTrue();
        options.UseIndexAliases.ShouldBeFalse();
        options.MaxRetries.ShouldBe(3);
        options.ConnectionTimeoutSeconds.ShouldBe(30);
        options.SlowQueryThresholdMs.ShouldBe(500);
        options.BulkBatchSize.ShouldBe(1000);
        options.EnableMultiTenancy.ShouldBeFalse();
        options.TenantIsolationMode.ShouldBe(TenantIsolationMode.IndexPerTenant);
        options.EnableHttpCompression.ShouldBeTrue();
    }

    [Fact]
    public void Options_ToString_MasksPasswordAndApiKey()
    {
        var options = new KyrolusElasticsearchOptions
        {
            Url = "https://elastic.production:9200",
            Username = "admin_user",
            Password = "UltraSecretPassword99!",
            ApiKey = "V2hpY2hJc1ZlcnlTZWNyZXRUb28="
        };

        var str = options.ToString();
        str.ShouldContain("User=admin_user");
        str.ShouldContain("Password=***");
        str.ShouldContain("ApiKey=***");
        str.ShouldNotContain("UltraSecretPassword99!");
        str.ShouldNotContain("V2hpY2hJc1ZlcnlTZWNyZXRUb28=");
    }

    [Fact]
    public void AddKyrolusElasticsearch_BindsConfigurationCorrectly()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "KyrolusElasticsearch:Url", "https://elastic-cluster.mycompany.com:9200" },
            { "KyrolusElasticsearch:DefaultIndex", "main-app" },
            { "KyrolusElasticsearch:Username", "elastic_user" },
            { "KyrolusElasticsearch:Password", "secret_pass" },
            { "KyrolusElasticsearch:AutoCreateIndices", "false" },
            { "KyrolusElasticsearch:SlowQueryThresholdMs", "250" },
            { "KyrolusElasticsearch:IndexPrefix", "dev_" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusElasticsearch(configuration.GetSection("KyrolusElasticsearch"));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KyrolusElasticsearchOptions>>().Value;

        options.Url.ShouldBe("https://elastic-cluster.mycompany.com:9200");
        options.DefaultIndex.ShouldBe("main-app");
        options.Username.ShouldBe("elastic_user");
        options.Password.ShouldBe("secret_pass");
        options.AutoCreateIndices.ShouldBeFalse();
        options.SlowQueryThresholdMs.ShouldBe(250);
        options.IndexPrefix.ShouldBe("dev_");

        var kyrolusIndexManager = provider.GetService<IKyrolusElasticIndexManager>();
        kyrolusIndexManager.ShouldNotBeNull();

        var indexManager = provider.GetService<IKyrolusElasticIndexManager>();
        indexManager.ShouldNotBeNull();

        var kyrolusRepo = provider.GetService<IKyrolusElasticRepository<TestProductDocument, string>>();
        kyrolusRepo.ShouldNotBeNull();
        kyrolusRepo.IndexName.ShouldBe("dev_products-live");

        var repo = provider.GetService<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.ShouldNotBeNull();
        repo.IndexName.ShouldBe("dev_products-live");
    }

    [Fact]
    public void MultiTenancy_ResolvesTenantIndexCorrectly()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusElasticsearch(options =>
        {
            options.EnableMultiTenancy = true;
            options.TenantIsolationMode = TenantIsolationMode.IndexPerTenant;
            options.IndexPrefix = "app_";
        });
        services.AddElasticsearchTenantProvider<TestTenantProvider>();

        var provider = services.BuildServiceProvider();
        var repo = provider.GetRequiredService<IKyrolusElasticRepository<TestProductDocument, string>>();

        repo.IndexName.ShouldBe("app_tenant_alpha_products-live");
    }

    [Fact]
    public void SearchResult_CalculatesDocumentsCorrectly()
    {
        var doc1 = new TestProductDocument { Id = "1", Title = "Phone", Price = 999 };
        var doc2 = new TestProductDocument { Id = "2", Title = "Laptop", Price = 1999 };

        var hit1 = new KyrolusSearchHit<TestProductDocument>(doc1, "1", 1.5);
        var hit2 = new KyrolusSearchHit<TestProductDocument>(doc2, "2", 0.8);

        var result = new KyrolusSearchResult<TestProductDocument>
        {
            Hits = [hit1, hit2],
            Total = 2,
            TookMs = 12,
            MaxScore = 1.5,
            Facets = new Dictionary<string, IReadOnlyList<KyrolusFacetBucket>>
            {
                { "categories", [new KyrolusFacetBucket("Electronics", 2)] }
            }
        };

        result.Documents.Count.ShouldBe(2);
        result.Documents[0].Title.ShouldBe("Phone");
        result.Documents[1].Title.ShouldBe("Laptop");
        result.Total.ShouldBe(2);
        result.TookMs.ShouldBe(12);
        result.MaxScore.ShouldBe(1.5);
        result.Facets.ContainsKey("categories").ShouldBeTrue();
        result.Facets["categories"][0].Key.ShouldBe("Electronics");
        result.Facets["categories"][0].DocCount.ShouldBe(2);
    }

    [Fact]
    public void BulkResult_CalculatesTotalsAndErrors_Properly()
    {
        var bulkResult = new KyrolusBulkResult
        {
            TotalCount = 10,
            IndexedCount = 8,
            FailedCount = 2,
            TookMs = 45,
            Errors = [
                new KyrolusBulkItemError("doc_1", 400, "Mapping parse exception"),
                new KyrolusBulkItemError("doc_2", 429, "Rate limit exceeded")
            ]
        };

        bulkResult.HasErrors.ShouldBeTrue();
        bulkResult.TotalCount.ShouldBe(10);
        bulkResult.IndexedCount.ShouldBe(8);
        bulkResult.FailedCount.ShouldBe(2);
        bulkResult.TookMs.ShouldBe(45);
        bulkResult.Errors.Count.ShouldBe(2);
    }

    [Fact]
    public void SmartSearchBuilder_AppliesCriteriaCorrectly()
    {
        var builder = new KyrolusSmartSearchBuilder<TestProductDocument>();

        builder
            .Search("iphone 15", p => p.Title, p => p.Description)
            .Fuzzy("AUTO", prefixLength: 2)
            .Highlight(p => p.Title, p => p.Description)
            .Filter(p => p.Category, "Smartphones")
            .FilterIn(p => p.Category, ["Smartphones", "Mobiles"])
            .Range(p => p.Price, min: 500, max: 1500)
            .DateRange(p => p.CreatedAt, from: DateTimeOffset.UtcNow.AddDays(-30), to: DateTimeOffset.UtcNow)
            .GeoDistance(p => p.Location, latitude: 30.0444, longitude: 31.2357, distanceKm: 10.0)
            .BoostWhen(p => p.IsFeatured, matchValue: true, boost: 2.5f)
            .OrderBy(p => p.Price, descending: true)
            .MinScore(0.5f)
            .Paginate(page: 2, pageSize: 15);

        var descriptor = new SearchRequestDescriptor<TestProductDocument>();
        Should.NotThrow(() => builder.Apply(descriptor));
    }

    [Fact]
    public async Task CachedElasticRepository_CachesGetById_AndInvalidatesOnMutation()
    {
        var mockCache = new TestMockCacheProvider();
        var mockRepo = new TestMockElasticRepository<TestProductDocument, string>("products");

        var cachedRepo = new KyrolusCachedElasticRepository<TestProductDocument, string>(mockRepo, mockCache);

        // 1. Initial Get (should query inner and populate cache)
        var doc = await cachedRepo.GetByIdAsync("p1");
        doc.ShouldNotBeNull();
        mockCache.Store.ContainsKey("es:products:doc:p1").ShouldBeTrue();

        // 2. Mutate (Update should invalidate cache)
        await cachedRepo.UpdateAsync(new TestProductDocument { Id = "p1", Title = "Updated" }, "p1");
        mockCache.Store.ContainsKey("es:products:doc:p1").ShouldBeFalse();
    }
}

public sealed class TestMockCacheProvider : IKyrolusCacheProvider
{
    public Dictionary<string, object> Store { get; } = [];

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (Store.TryGetValue(cacheKey, out var val) && val is T typedVal)
            return Task.FromResult<T?>(typedVal);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        if (value is not null) Store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        if (value is not null) Store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        Store.Remove(cacheKey);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default) =>
        Task.FromResult(Store.ContainsKey(cacheKey));

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) =>
        Task.FromResult<IDictionary<string, T?>>(new Dictionary<string, T?>());

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task<T> GetOrCreateAsync<T>(string cacheKey, Func<CancellationToken, Task<T>> factory, KyrolusCacheEntryOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Store.TryGetValue(cacheKey, out var val) && val is T typedVal)
            return typedVal;

        var created = await factory(cancellationToken);
        if (created is not null) Store[cacheKey] = created;
        return created;
    }
}

public sealed class TestMockElasticRepository<TDocument, TId>(string indexName) : IKyrolusElasticRepository<TDocument, TId> where TDocument : class, new()
{
    public string IndexName => indexName;

    public Task<bool> AddAsync(TDocument document, TId id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<int> AddManyAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default) => Task.FromResult(items.Count());
    public Task<KyrolusBulkResult> BulkIndexAsync(IEnumerable<(TDocument Document, TId Id)> items, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusBulkResult { IndexedCount = items.Count() });
    public Task<TDocument?> GetByIdAsync(TId id, CancellationToken cancellationToken = default) => Task.FromResult<TDocument?>(new TDocument());
    public Task<IReadOnlyList<TDocument>> GetManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TDocument>>([]);
    public Task<bool> UpdateAsync(TDocument document, TId id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> UpdatePartialAsync(TId id, object partialDocument, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> UpdateByScriptAsync(TId id, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> DeleteAsync(TId id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default) => Task.FromResult((long)ids.Count());
    public Task<KyrolusBulkResult> BulkDeleteAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusBulkResult { IndexedCount = ids.Count() });
    public Task<long> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
    public Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<KyrolusSearchResult<TDocument>> SearchAsync(Action<SearchRequestDescriptor<TDocument>> configureSearch, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> VectorSearchAsync(float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> HybridSearchAsync(string queryText, float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, Expression<Func<TDocument, object>> field, int limit = 5, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<KyrolusPointInTime> OpenPointInTimeAsync(TimeSpan keepAlive, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusPointInTime("pit_1", keepAlive));
    public Task<bool> ClosePointInTimeAsync(string pitId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<KyrolusSearchResult<TDocument>> SearchAfterAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, IReadOnlyList<object>? searchAfterValues, string? pitId = null, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());

    public async IAsyncEnumerable<TDocument> StreamAllAsync(Action<KyrolusSmartSearchBuilder<TDocument>>? configure = null, int batchSize = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new TDocument();
        await Task.CompletedTask;
    }
}
