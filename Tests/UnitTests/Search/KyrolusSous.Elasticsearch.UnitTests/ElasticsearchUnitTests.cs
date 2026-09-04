using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Text;
using Elastic.Clients.Elasticsearch;
using Elastic.Clients.Elasticsearch.Aggregations;
using Elastic.Transport;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Elasticsearch.UnitTests;

[KyrolusElasticIndex("products", NumberOfShards = 3, NumberOfReplicas = 2, UseAlias = true, Alias = "products-live", RoutingField = "Category")]
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

    [KyrolusElasticCompletion]
    public string? TitleSuggest { get; set; }
}

public class TestTenantProvider : IKyrolusTenantProvider
{
    public string? CurrentTenantId => "tenant_alpha";
}

public class ElasticsearchUnitTests
{
    #region Test infrastructure: a real ElasticsearchClient wired to Elastic.Transport's InMemoryRequestInvoker

    // These tests run KyrolusElasticRepository<TDocument, TId> against a real ElasticsearchClient whose
    // transport never touches the network - Elastic.Transport.InMemoryRequestInvoker returns a canned
    // response instead. CapturingRequestInvoker wraps it to also record the exact outgoing request
    // (path + query string, and body) so tests can assert on what the client actually would have sent to
    // a cluster - e.g. that a query body contains "simple_query_string" and not "query_string" (Fix 1),
    // or that an update/delete request's query string does/doesn't carry if_seq_no/if_primary_term
    // (Fix 4). This is not a live-cluster integration test; it verifies request construction only.
    private sealed class CapturingRequestInvoker(IRequestInvoker inner) : IRequestInvoker
    {
        public string? LastPathAndQuery { get; private set; }
        public byte[]? LastRequestBody { get; private set; }

        public ResponseFactory ResponseFactory => inner.ResponseFactory;

        public TResponse Request<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
            where TResponse : TransportResponse, new()
        {
            Capture(endpoint, boundConfiguration, postData);
            return inner.Request<TResponse>(endpoint, boundConfiguration, postData);
        }

        public async Task<TResponse> RequestAsync<TResponse>(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData, CancellationToken cancellationToken)
            where TResponse : TransportResponse, new()
        {
            Capture(endpoint, boundConfiguration, postData);
            return await inner.RequestAsync<TResponse>(endpoint, boundConfiguration, postData, cancellationToken);
        }

        public void Dispose() => inner.Dispose();

        private void Capture(Endpoint endpoint, BoundConfiguration boundConfiguration, PostData? postData)
        {
            LastPathAndQuery = endpoint.PathAndQuery;
            if (postData is null)
            {
                LastRequestBody = null;
                return;
            }

            using var ms = new MemoryStream();
            postData.Write(ms, boundConfiguration.ConnectionSettings, boundConfiguration.DisableDirectStreaming);
            LastRequestBody = ms.ToArray();
        }
    }

    private const string EmptySearchResponseJson =
        """{"took":1,"timed_out":false,"_shards":{"total":1,"successful":1,"skipped":0,"failed":0},"hits":{"total":{"value":0,"relation":"eq"},"max_score":null,"hits":[]}}""";

    private const string UpdateSuccessResponseJson =
        """{"_index":"products","_id":"doc-1","_version":2,"result":"updated","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":5,"_primary_term":1}""";

    private const string DeleteSuccessResponseJson =
        """{"_index":"products","_id":"doc-1","_version":3,"result":"deleted","_shards":{"total":2,"successful":1,"failed":0},"_seq_no":6,"_primary_term":1}""";

    private const string VersionConflictResponseJson =
        """{"error":{"root_cause":[{"type":"version_conflict_engine_exception","reason":"version conflict"}],"type":"version_conflict_engine_exception","reason":"version conflict, required seqNo does not match","status":409},"status":409}""";

    private static (KyrolusElasticRepository<TestProductDocument, string> Repo, CapturingRequestInvoker Capture) CreateRepoWithCannedResponse(
        string responseJson,
        int statusCode = 200)
    {
        var headers = new Dictionary<string, IEnumerable<string>> { ["x-elastic-product"] = ["Elasticsearch"] };
        var inMemory = new InMemoryRequestInvoker(Encoding.UTF8.GetBytes(responseJson), statusCode: statusCode, headers: headers);
        var capturing = new CapturingRequestInvoker(inMemory);
        var settings = new ElasticsearchClientSettings(new SingleNodePool(new Uri("http://localhost:9200")), capturing)
            .DefaultIndex("products")
            .DisableDirectStreaming();
        var client = new ElasticsearchClient(settings);
        var repo = new KyrolusElasticRepository<TestProductDocument, string>(client, Options.Create(new KyrolusElasticsearchOptions()));
        return (repo, capturing);
    }

    #endregion

    [Fact(DisplayName = "Elastic Index Attribute Reads Properties Correctly")]
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
        attr.RoutingField.ShouldBe("Category");
    }

    [Fact(DisplayName = "Elastic Mapping Attributes Read Metadata Correctly")]
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

        var completionAttr = typeof(TestProductDocument).GetProperty("TitleSuggest")?
            .GetCustomAttributes(typeof(KyrolusElasticCompletionAttribute), false)
            .Cast<KyrolusElasticCompletionAttribute>()
            .FirstOrDefault();

        completionAttr.ShouldNotBeNull();
        completionAttr.Analyzer.ShouldBe("simple");
    }

    [Fact(DisplayName = "Kyrolus Elasticsearch Options Defaults Are Valid")]
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

    [Fact(DisplayName = "Options To String Masks Password And Api Key")]
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

    [Fact(DisplayName = "Add Kyrolus Elasticsearch Binds Configuration Correctly")]
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

        var kyrolusSnapshotManager = provider.GetService<IKyrolusElasticSnapshotManager>();
        kyrolusSnapshotManager.ShouldNotBeNull();

        var kyrolusSynonymManager = provider.GetService<IKyrolusElasticSynonymManager>();
        kyrolusSynonymManager.ShouldNotBeNull();

        var kyrolusRepo = provider.GetService<IKyrolusElasticRepository<TestProductDocument, string>>();
        kyrolusRepo.ShouldNotBeNull();
        kyrolusRepo.IndexName.ShouldBe("dev_products-live");
    }

    [Fact(DisplayName = "Multi Tenancy Resolves Tenant Index Correctly")]
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

    [Fact(DisplayName = "Search Result Rich Analytics Calculates Properly")]
    public void SearchResult_RichAnalytics_CalculatesProperly()
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
            },
            Histograms = new Dictionary<string, IReadOnlyList<KyrolusHistogramBucket>>
            {
                { "price_hist", [new KyrolusHistogramBucket(1000, 2)] }
            },
            Stats = new Dictionary<string, KyrolusStatsResult>
            {
                { "price_stats", new KyrolusStatsResult(2, 999, 1999, 1499, 2998) }
            },
            ExtendedStats = new Dictionary<string, KyrolusExtendedStatsResult>
            {
                { "price_ext_stats", new KyrolusExtendedStatsResult(2, 999, 1999, 1499, 2998, 4994002, 500000, 707.1) }
            },
            Cardinalities = new Dictionary<string, long>
            {
                { "unique_categories", 1 }
            },
            Suggestions = new Dictionary<string, IReadOnlyList<KyrolusSuggestOption>>
            {
                { "title_suggest", [new KyrolusSuggestOption("iPhone 15 Pro", 1.0, null, null)] }
            }
        };

        result.Documents.Count.ShouldBe(2);
        result.Documents[0].Title.ShouldBe("Phone");
        result.Documents[1].Title.ShouldBe("Laptop");
        result.Total.ShouldBe(2);
        result.TookMs.ShouldBe(12);
        result.MaxScore.ShouldBe(1.5);
        result.Facets["categories"][0].Key.ShouldBe("Electronics");
        result.Histograms["price_hist"][0].Key.ShouldBe(1000);
        result.Stats["price_stats"].Avg.ShouldBe(1499);
        result.ExtendedStats["price_ext_stats"].Variance.ShouldBe(500000);
        result.Cardinalities["unique_categories"].ShouldBe(1);
        result.Suggestions["title_suggest"][0].Text.ShouldBe("iPhone 15 Pro");
    }

    [Fact(DisplayName = "Bulk Result Calculates Totals And Errors Properly")]
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

    [Fact(DisplayName = "Smart Search Builder Applies Criteria Aggregations And Suggesters Correctly")]
    public void SmartSearchBuilder_AppliesCriteria_Aggregations_And_Suggesters_Correctly()
    {
        var builder = new KyrolusSmartSearchBuilder<TestProductDocument>();

        builder
            .Search("iphone 15", p => p.Title, p => p.Description)
            .Fuzzy("AUTO", prefixLength: 2)
            .Routing("tenant_electronics")
            .Highlight(p => p.Title, p => p.Description)
            .Filter(p => p.Category, "Smartphones")
            .FilterIn(p => p.Category, ["Smartphones", "Mobiles"])
            .Range(p => p.Price, min: 500, max: 1500)
            .DateRange(p => p.CreatedAt, from: DateTimeOffset.UtcNow.AddDays(-30), to: DateTimeOffset.UtcNow)
            .GeoDistance(p => p.Location, latitude: 30.0444, longitude: 31.2357, distanceKm: 10.0)
            .BoostWhen(p => p.IsFeatured, matchValue: true, boost: 2.5f)
            .OrderBy(p => p.Price, descending: true)
            .MinScore(0.5f)
            .Paginate(page: 2, pageSize: 15)
            .TermsAggregation("cat_terms", p => p.Category, size: 20)
            .HistogramAggregation("price_hist", p => p.Price, interval: 100)
            .DateHistogramAggregation("created_hist", p => p.CreatedAt, CalendarInterval.Month)
            .StatsAggregation("price_stats", p => p.Price)
            .ExtendedStatsAggregation("price_ext_stats", p => p.Price)
            .CardinalityAggregation("cat_cardinality", p => p.Category)
            .PercentilesAggregation("price_pct", p => p.Price, 50, 90, 99)
            .RangeAggregation("price_ranges", p => p.Price, (0, 500), (500, 1000), (1000, null))
            .SuggestPhrase("phrase_suggester", "iphne 15", p => p.Title)
            .SuggestTerm("term_suggester", "iphne", p => p.Title)
            .SuggestCompletion("completion_suggester", "iph", p => p.TitleSuggest, fuzzy: true);

        var descriptor = new SearchRequestDescriptor<TestProductDocument>();
        Should.NotThrow(() => builder.Apply(descriptor));
    }

    [Fact(DisplayName = "Smart Search Builder Applies Delete And Update Descriptors Correctly")]
    public void SmartSearchBuilder_AppliesDeleteAndUpdateDescriptors_Correctly()
    {
        var builder = new KyrolusSmartSearchBuilder<TestProductDocument>();
        builder
            .Search("obsolete", p => p.Title)
            .Filter(p => p.Category, "Discontinued")
            .Routing("tenant_discontinued");

        var deleteDescriptor = new DeleteByQueryRequestDescriptor<TestProductDocument>("products");
        Should.NotThrow(() => builder.Apply(deleteDescriptor));

        var updateDescriptor = new UpdateByQueryRequestDescriptor<TestProductDocument>("products");
        Should.NotThrow(() => builder.Apply(updateDescriptor));
    }

    [Fact(DisplayName = "Regression (Fix 1 - query_string injection/DoS): a free-text search with no fields specified uses simple_query_string, not the injectable query_string")]
    public async Task SmartSearchBuilder_NoFieldsSearch_UsesSimpleQueryString_NotInjectableQueryString()
    {
        // KyrolusSmartSearchBuilder<T>.Search(text) with no field arguments falls into the "no _searchFields"
        // branch of Apply(). query_string parses its input as a Lucene mini-query-language (field:value
        // scoping, wildcards/regex, boosting, _exists_:field...) so raw end-user search text must never reach
        // it unescaped. simple_query_string never throws on malformed input and does not support that
        // dangerous syntax - it treats the whole payload as plain text/terms instead of a query language.
        var (repo, capture) = CreateRepoWithCannedResponse(EmptySearchResponseJson);

        await repo.SmartSearchAsync(b => b.Search("field_not_meant_to_be_searchable:leaked OR admin:true", Array.Empty<string>()));

        capture.LastRequestBody.ShouldNotBeNull();
        var body = Encoding.UTF8.GetString(capture.LastRequestBody!);
        body.ShouldContain("\"simple_query_string\"");
        body.ShouldNotContain("\"query_string\"");
    }

    [Fact(DisplayName = "Regression (Fix 1 - query_string injection/DoS): DeleteByQuery with no fields specified also uses simple_query_string")]
    public async Task SmartSearchBuilder_ApplyToDeleteByQuery_NoFieldsSearch_UsesSimpleQueryString_NotInjectableQueryString()
    {
        // DeleteByQuery is the higher-stakes half of Fix 1: an injected query_string here changes which
        // documents get deleted, not just which get returned.
        const string deleteByQueryResponseJson =
            """{"took":1,"timed_out":false,"total":0,"deleted":0,"batches":1,"version_conflicts":0,"noops":0,"retries":{"bulk":0,"search":0},"throttled_millis":0,"requests_per_second":-1.0,"throttled_until_millis":0,"failures":[]}""";
        var (repo, capture) = CreateRepoWithCannedResponse(deleteByQueryResponseJson);

        await repo.DeleteByQueryAsync(b => b.Search("field_not_meant_to_be_searchable:leaked OR admin:true", Array.Empty<string>()));

        capture.LastRequestBody.ShouldNotBeNull();
        var body = Encoding.UTF8.GetString(capture.LastRequestBody!);
        body.ShouldContain("\"simple_query_string\"");
        body.ShouldNotContain("\"query_string\"");
    }

    [Fact(DisplayName = "Regression (Fix 1 - query_string injection/DoS): UpdateByQuery with no fields specified also uses simple_query_string")]
    public async Task SmartSearchBuilder_ApplyToUpdateByQuery_NoFieldsSearch_UsesSimpleQueryString_NotInjectableQueryString()
    {
        const string updateByQueryResponseJson =
            """{"took":1,"timed_out":false,"total":0,"updated":0,"batches":1,"version_conflicts":0,"noops":0,"retries":{"bulk":0,"search":0},"throttled_millis":0,"requests_per_second":-1.0,"throttled_until_millis":0,"failures":[]}""";
        var (repo, capture) = CreateRepoWithCannedResponse(updateByQueryResponseJson);

        await repo.UpdateByQueryAsync(b => b.Search("field_not_meant_to_be_searchable:leaked OR admin:true", Array.Empty<string>()), "ctx._source.price = 0");

        capture.LastRequestBody.ShouldNotBeNull();
        var body = Encoding.UTF8.GetString(capture.LastRequestBody!);
        body.ShouldContain("\"simple_query_string\"");
        body.ShouldNotContain("\"query_string\"");
    }

    [Fact(DisplayName = "Cached Elastic Repository Caches Get By Id And Invalidates On Mutation")]
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

        // 3. MultiSearch & Suggesters should delegate to inner
        var multiRes = await cachedRepo.MultiSearchAsync([b => b.Search("phone", p => p.Title)]);
        multiRes.ShouldNotBeNull();

        var suggestRes = await cachedRepo.SuggestAsync(b => b.SuggestTerm("sug", "iphne", p => p.Title));
        suggestRes.ShouldNotBeNull();

        // 4. RrfSearch should delegate to inner
        var rrfRes = await cachedRepo.RrfSearchAsync(b => b.Search("iphone", p => p.Title), [0.1f, 0.2f]);
        rrfRes.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Bulk Buffer Enqueues And Flushes Successfully")]
    public async Task BulkBuffer_EnqueuesAndFlushes_Successfully()
    {
        var mockRepo = new TestMockElasticRepository<TestProductDocument, string>("products");
        var options = Options.Create(new KyrolusElasticBulkBufferOptions
        {
            BatchSize = 5,
            ChannelCapacity = 100,
            FlushInterval = TimeSpan.FromSeconds(1)
        });

        var buffer = new KyrolusElasticsearchBulkBuffer<TestProductDocument, string>(mockRepo, options);

        var enqueued = await buffer.EnqueueAsync(new TestProductDocument { Id = "buf1", Title = "Buff1" }, "buf1");
        enqueued.ShouldBeTrue();

        var manyCount = await buffer.EnqueueManyAsync([
            (new TestProductDocument { Id = "buf2", Title = "Buff2" }, "buf2"),
            (new TestProductDocument { Id = "buf3", Title = "Buff3" }, "buf3")
        ]);
        manyCount.ShouldBe(2);

        await Should.NotThrowAsync(() => buffer.FlushAsync());
    }

    #region Regression (Fix 4 - no optimistic concurrency on write paths)

    // These tests verify request construction (via CapturingRequestInvoker, see the "Test infrastructure"
    // region above) and the repository's translation of Elasticsearch's HTTP 409 version-conflict response
    // into a `false` return - not a genuine two-writer race against a live cluster/document, which this
    // project has no fixture for. That is called out explicitly in each test's own comments.

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): UpdateAsync omits if_seq_no/if_primary_term when neither is supplied (unchanged default behavior)")]
    public async Task UpdateAsync_NoConcurrencyTokens_OmitsIfSeqNoAndIfPrimaryTermFromRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(UpdateSuccessResponseJson);

        var ok = await repo.UpdateAsync(new TestProductDocument { Id = "doc-1", Title = "Updated" }, "doc-1");

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldNotContain("if_seq_no");
        capture.LastPathAndQuery.ShouldNotContain("if_primary_term");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): UpdateAsync carries if_seq_no/if_primary_term on the request when both are supplied")]
    public async Task UpdateAsync_WithConcurrencyTokens_CarriesIfSeqNoAndIfPrimaryTermOnRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(UpdateSuccessResponseJson);

        var ok = await repo.UpdateAsync(new TestProductDocument { Id = "doc-1", Title = "Updated" }, "doc-1", ifSeqNo: 5, ifPrimaryTerm: 1);

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldContain("if_seq_no=5");
        capture.LastPathAndQuery.ShouldContain("if_primary_term=1");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): UpdatePartialAsync omits if_seq_no/if_primary_term when neither is supplied (unchanged default behavior)")]
    public async Task UpdatePartialAsync_NoConcurrencyTokens_OmitsIfSeqNoAndIfPrimaryTermFromRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(UpdateSuccessResponseJson);

        var ok = await repo.UpdatePartialAsync("doc-1", new { Title = "Updated" });

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldNotContain("if_seq_no");
        capture.LastPathAndQuery.ShouldNotContain("if_primary_term");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): UpdatePartialAsync carries if_seq_no/if_primary_term on the request when both are supplied")]
    public async Task UpdatePartialAsync_WithConcurrencyTokens_CarriesIfSeqNoAndIfPrimaryTermOnRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(UpdateSuccessResponseJson);

        var ok = await repo.UpdatePartialAsync("doc-1", new { Title = "Updated" }, ifSeqNo: 5, ifPrimaryTerm: 1);

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldContain("if_seq_no=5");
        capture.LastPathAndQuery.ShouldContain("if_primary_term=1");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): DeleteAsync omits if_seq_no/if_primary_term when neither is supplied (unchanged default behavior)")]
    public async Task DeleteAsync_NoConcurrencyTokens_OmitsIfSeqNoAndIfPrimaryTermFromRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(DeleteSuccessResponseJson);

        var ok = await repo.DeleteAsync("doc-1");

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldNotContain("if_seq_no");
        capture.LastPathAndQuery.ShouldNotContain("if_primary_term");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): DeleteAsync carries if_seq_no/if_primary_term on the request when both are supplied")]
    public async Task DeleteAsync_WithConcurrencyTokens_CarriesIfSeqNoAndIfPrimaryTermOnRequest()
    {
        var (repo, capture) = CreateRepoWithCannedResponse(DeleteSuccessResponseJson);

        var ok = await repo.DeleteAsync("doc-1", ifSeqNo: 6, ifPrimaryTerm: 1);

        ok.ShouldBeTrue();
        capture.LastPathAndQuery.ShouldNotBeNull();
        capture.LastPathAndQuery.ShouldContain("if_seq_no=6");
        capture.LastPathAndQuery.ShouldContain("if_primary_term=1");
    }

    [Fact(DisplayName = "Regression (Fix 4 - optimistic concurrency): a stale seq_no/primary_term is rejected as a version conflict (false), not silently applied as an overwrite")]
    public async Task UpdatePartialAsync_StaleConcurrencyTokens_ReturnsFalse_NotSilentOverwrite()
    {
        // No live Elasticsearch cluster is available in this test project, so a genuine two-writer race
        // (both readers fetching the same seq_no/primary_term, one writing, the second losing) cannot be
        // reproduced end-to-end here. What IS verified: (1) a "first write" call against a repo wired to
        // return 200 succeeds, simulating the winner of the race using the seq_no/primary_term it captured
        // before either write; (2) a "second write" call using that SAME now-stale pair, against a repo wired
        // to return Elasticsearch's real version_conflict_engine_exception/409 shape, returns false rather
        // than throwing or silently reporting success - proving the lost-update problem described in Fix 4
        // (two concurrent partial updates silently overwriting each other) no longer happens once a caller
        // opts in to supplying ifSeqNo/ifPrimaryTerm.
        var (firstWriterRepo, _) = CreateRepoWithCannedResponse(UpdateSuccessResponseJson);
        var firstWriteSucceeded = await firstWriterRepo.UpdatePartialAsync("doc-1", new { Price = 100m }, ifSeqNo: 5, ifPrimaryTerm: 1);
        firstWriteSucceeded.ShouldBeTrue();

        var (secondWriterRepo, secondCapture) = CreateRepoWithCannedResponse(VersionConflictResponseJson, statusCode: 409);
        var secondWriteSucceeded = await secondWriterRepo.UpdatePartialAsync("doc-1", new { Price = 200m }, ifSeqNo: 5, ifPrimaryTerm: 1);

        secondWriteSucceeded.ShouldBeFalse();
        secondCapture.LastPathAndQuery.ShouldNotBeNull();
        secondCapture.LastPathAndQuery.ShouldContain("if_seq_no=5");
        secondCapture.LastPathAndQuery.ShouldContain("if_primary_term=1");
    }

    #endregion
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
    public Task<bool> UpdateAsync(TDocument document, TId id, long? ifSeqNo = null, long? ifPrimaryTerm = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> UpdatePartialAsync(TId id, object partialDocument, long? ifSeqNo = null, long? ifPrimaryTerm = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> UpdateByScriptAsync(TId id, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<bool> DeleteAsync(TId id, long? ifSeqNo = null, long? ifPrimaryTerm = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<long> DeleteManyAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default) => Task.FromResult((long)ids.Count());
    public Task<KyrolusBulkResult> BulkDeleteAsync(IEnumerable<TId> ids, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusBulkResult { IndexedCount = ids.Count() });
    public Task<long> CountAsync(CancellationToken cancellationToken = default) => Task.FromResult(1L);
    public Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<KyrolusSearchResult<TDocument>> SearchAsync(Action<SearchRequestDescriptor<TDocument>> configureSearch, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> SmartSearchAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<IReadOnlyList<KyrolusSearchResult<TDocument>>> MultiSearchAsync(IEnumerable<Action<KyrolusSmartSearchBuilder<TDocument>>> searchActions, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KyrolusSearchResult<TDocument>>>([]);
    public Task<KyrolusSearchResult<TDocument>> VectorSearchAsync(float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> HybridSearchAsync(string queryText, float[] vector, string vectorField = "embedding", int topK = 10, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<KyrolusSearchResult<TDocument>> RrfSearchAsync(Action<KyrolusSmartSearchBuilder<TDocument>> textQuery, float[] vector, string vectorField = "embedding", int topK = 10, int windowSize = 50, int rankConstant = 60, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());
    public Task<IReadOnlyList<string>> AutocompleteAsync(string prefix, Expression<Func<TDocument, object>> field, int limit = 5, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
    public Task<IDictionary<string, IReadOnlyList<KyrolusSuggestOption>>> SuggestAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, CancellationToken cancellationToken = default) => Task.FromResult<IDictionary<string, IReadOnlyList<KyrolusSuggestOption>>>(new Dictionary<string, IReadOnlyList<KyrolusSuggestOption>>());
    public Task<KyrolusByQueryResult> DeleteByQueryAsync(Action<KyrolusSmartSearchBuilder<TDocument>> filter, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusByQueryResult(0, 0, 0, 0, 0, 0, 0));
    public Task<KyrolusByQueryResult> UpdateByQueryAsync(Action<KyrolusSmartSearchBuilder<TDocument>> filter, string script, Dictionary<string, object>? parameters = null, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusByQueryResult(0, 0, 0, 0, 0, 0, 0));
    public Task<bool> RegisterPercolateQueryAsync(string queryId, Action<KyrolusSmartSearchBuilder<TDocument>> query, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateDocumentAsync(TDocument document, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KyrolusPercolateMatch>>([]);
    public Task<IReadOnlyList<KyrolusPercolateMatch>> PercolateExistingDocumentAsync(TId id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<KyrolusPercolateMatch>>([]);
    public Task<KyrolusTaskStatus?> GetTaskStatusAsync(string taskId, CancellationToken cancellationToken = default) => Task.FromResult<KyrolusTaskStatus?>(new KyrolusTaskStatus(taskId, true, "test", null, null));
    public Task<KyrolusPointInTime> OpenPointInTimeAsync(TimeSpan keepAlive, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusPointInTime("pit_1", keepAlive));
    public Task<bool> ClosePointInTimeAsync(string pitId, CancellationToken cancellationToken = default) => Task.FromResult(true);
    public Task<KyrolusSearchResult<TDocument>> SearchAfterAsync(Action<KyrolusSmartSearchBuilder<TDocument>> build, IReadOnlyList<object>? searchAfterValues, string? pitId = null, CancellationToken cancellationToken = default) => Task.FromResult(new KyrolusSearchResult<TDocument>());

    public async IAsyncEnumerable<TDocument> StreamAllAsync(Action<KyrolusSmartSearchBuilder<TDocument>>? configure = null, int batchSize = 1000, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new TDocument();
        await Task.CompletedTask;
    }
}
