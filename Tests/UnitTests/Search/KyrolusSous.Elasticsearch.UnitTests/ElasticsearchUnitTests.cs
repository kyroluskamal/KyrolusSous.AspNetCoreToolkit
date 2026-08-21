using Elastic.Clients.Elasticsearch;
using KyrolusSous.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Elasticsearch.UnitTests;

[ElasticIndex("products", NumberOfShards = 3, NumberOfReplicas = 2, UseAlias = true, Alias = "products-live")]
[SyncToElasticsearch(IndexName = "products", IdProperty = "Id")]
public class TestProductDocument
{
    public string Id { get; set; } = string.Empty;

    [ElasticText(Analyzer = "arabic")]
    public string Title { get; set; } = string.Empty;

    [ElasticText]
    public string Description { get; set; } = string.Empty;

    [ElasticKeyword]
    public string Category { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsFeatured { get; set; }

    [ElasticGeoPoint]
    public GeoCoordinate? Location { get; set; }

    [ElasticDenseVector(1536)]
    public float[]? Embedding { get; set; }
}

public class TestTenantProvider : ITenantProvider
{
    public string? CurrentTenantId => "tenant_alpha";
}

public class ElasticsearchUnitTests
{
    [Fact]
    public void ElasticIndexAttribute_ReadsPropertiesCorrectly()
    {
        var attr = typeof(TestProductDocument).GetCustomAttributes(typeof(ElasticIndexAttribute), false)
            .Cast<ElasticIndexAttribute>()
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
            .GetCustomAttributes(typeof(ElasticTextAttribute), false)
            .Cast<ElasticTextAttribute>()
            .FirstOrDefault();

        titleAttr.ShouldNotBeNull();
        titleAttr.Analyzer.ShouldBe("arabic");

        var catAttr = typeof(TestProductDocument).GetProperty("Category")?
            .GetCustomAttributes(typeof(ElasticKeywordAttribute), false)
            .Cast<ElasticKeywordAttribute>()
            .FirstOrDefault();

        catAttr.ShouldNotBeNull();
        catAttr.Index.ShouldBeTrue();

        var vectorAttr = typeof(TestProductDocument).GetProperty("Embedding")?
            .GetCustomAttributes(typeof(ElasticDenseVectorAttribute), false)
            .Cast<ElasticDenseVectorAttribute>()
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

        var indexManager = provider.GetService<IElasticIndexManager>();
        indexManager.ShouldNotBeNull();

        var repo = provider.GetService<IElasticRepository<TestProductDocument, string>>();
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
        var repo = provider.GetRequiredService<IElasticRepository<TestProductDocument, string>>();

        repo.IndexName.ShouldBe("app_tenant_alpha_products-live");
    }

    [Fact]
    public void SearchResult_CalculatesDocumentsCorrectly()
    {
        var doc1 = new TestProductDocument { Id = "1", Title = "Phone", Price = 999 };
        var doc2 = new TestProductDocument { Id = "2", Title = "Laptop", Price = 1999 };

        var hit1 = new SearchHit<TestProductDocument>(doc1, "1", 1.5);
        var hit2 = new SearchHit<TestProductDocument>(doc2, "2", 0.8);

        var result = new SearchResult<TestProductDocument>
        {
            Hits = [hit1, hit2],
            Total = 2,
            TookMs = 12,
            MaxScore = 1.5,
            Facets = new Dictionary<string, IReadOnlyList<FacetBucket>>
            {
                { "categories", [new FacetBucket("Electronics", 2)] }
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
    public void SmartSearchBuilder_AppliesCriteriaCorrectly()
    {
        var builder = new SmartSearchBuilder<TestProductDocument>();

        builder
            .Search("iphone 15", p => p.Title, p => p.Description)
            .Fuzzy("AUTO", prefixLength: 2)
            .Filter(p => p.Category, "Smartphones")
            .FilterIn(p => p.Category, ["Smartphones", "Mobiles"])
            .Range(p => p.Price, min: 500, max: 1500)
            .GeoDistance(p => p.Location, latitude: 30.0444, longitude: 31.2357, distanceKm: 10.0)
            .BoostWhen(p => p.IsFeatured, matchValue: true, boost: 2.5f)
            .OrderBy(p => p.Price, descending: true)
            .MinScore(0.5f)
            .Paginate(page: 2, pageSize: 15);

        var descriptor = new SearchRequestDescriptor<TestProductDocument>();
        Should.NotThrow(() => builder.Apply(descriptor));
    }
}
