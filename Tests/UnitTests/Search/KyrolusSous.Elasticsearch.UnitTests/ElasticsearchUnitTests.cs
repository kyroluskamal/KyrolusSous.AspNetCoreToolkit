using KyrolusSous.Elasticsearch;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace KyrolusSous.Elasticsearch.UnitTests;

[ElasticIndex("products", NumberOfShards = 3, NumberOfReplicas = 2, UseAlias = true, Alias = "products-live")]
public class TestProductDocument
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
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
            MaxScore = 1.5
        };

        result.Documents.Count.ShouldBe(2);
        result.Documents[0].Title.ShouldBe("Phone");
        result.Documents[1].Title.ShouldBe("Laptop");
        result.Total.ShouldBe(2);
        result.TookMs.ShouldBe(12);
        result.MaxScore.ShouldBe(1.5);
    }
}
