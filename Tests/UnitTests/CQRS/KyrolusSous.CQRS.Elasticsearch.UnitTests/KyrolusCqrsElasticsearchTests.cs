using System.Reflection;
using Elastic.Clients.Elasticsearch;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.Projections;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Elasticsearch.Command;
using KyrolusSous.CQRS.Elasticsearch.Config;
using KyrolusSous.CQRS.Elasticsearch.Projections;
using KyrolusSous.CQRS.Elasticsearch.Query;
using KyrolusSous.Elasticsearch;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace KyrolusSous.CQRS.Elasticsearch.UnitTests;

public class KyrolusCqrsElasticsearchTests
{
    public sealed class TestProductDocument
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    [Fact(DisplayName = "ElasticSearchQueryHandler executes smart search with pagination and sorting")]
    public async Task ElasticSearchQueryHandler_ExecutesSmartSearch()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var expectedResult = new KyrolusSearchResult<TestProductDocument>
        {
            Total = 1,
            Hits = [new KyrolusSearchHit<TestProductDocument>(new TestProductDocument { Id = "p-1", Title = "Laptop", Price = 999m }, "p-1")]
        };

        repo.SmartSearchAsync(Arg.Any<Action<KyrolusSmartSearchBuilder<TestProductDocument>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResult));

        var handler = new ElasticSearchQueryHandler<TestProductDocument, string>(repo);
        var query = new ElasticSearchQuery<TestProductDocument>("Laptop", Page: 2, PageSize: 15, EnableFuzzy: true)
        {
            Fields = ["title", "description"],
            SortField = "price",
            SortDescending = true,
            HighlightFields = ["title"]
        };

        var result = await handler.Handle(query, CancellationToken.None);

        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Documents.Count.ShouldBe(1);
        result.Documents[0].Title.ShouldBe("Laptop");

        await repo.Received(1).SmartSearchAsync(Arg.Any<Action<KyrolusSmartSearchBuilder<TestProductDocument>>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticAutocompleteQueryHandler returns suggestions matching prefix")]
    public async Task ElasticAutocompleteQueryHandler_ReturnsSuggestions()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var searchResult = new KyrolusSearchResult<TestProductDocument>
        {
            Hits =
            [
                new KyrolusSearchHit<TestProductDocument>(new TestProductDocument { Id = "1", Title = "iPhone 15" }, "1"),
                new KyrolusSearchHit<TestProductDocument>(new TestProductDocument { Id = "2", Title = "iPhone 16" }, "2")
            ]
        };

        repo.SearchAsync(Arg.Any<Action<SearchRequestDescriptor<TestProductDocument>>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(searchResult));

        var handler = new ElasticAutocompleteQueryHandler<TestProductDocument, string>(repo);
        var query = new ElasticAutocompleteQuery<TestProductDocument>("iPh", TargetField: "Title", MaxSuggestions: 5);

        var suggestions = await handler.Handle(query, CancellationToken.None);

        suggestions.Count.ShouldBe(2);
        suggestions.ShouldContain("iPhone 15");
        suggestions.ShouldContain("iPhone 16");
    }

    [Fact(DisplayName = "ElasticAutocompleteQueryHandler returns empty on blank prefix")]
    public async Task ElasticAutocompleteQueryHandler_EmptyOnBlankPrefix()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var handler = new ElasticAutocompleteQueryHandler<TestProductDocument, string>(repo);
        var query = new ElasticAutocompleteQuery<TestProductDocument>("  ", TargetField: "Title");

        var suggestions = await handler.Handle(query, CancellationToken.None);

        suggestions.ShouldBeEmpty();
        await repo.DidNotReceive().SearchAsync(Arg.Any<Action<SearchRequestDescriptor<TestProductDocument>>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticVectorSearchQueryHandler delegates to VectorSearchAsync")]
    public async Task ElasticVectorSearchQueryHandler_DelegatesToRepository()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var expectedResult = new KyrolusSearchResult<TestProductDocument>
        {
            Total = 1,
            Hits = [new KyrolusSearchHit<TestProductDocument>(new TestProductDocument { Id = "v-1", Title = "Neural Net" }, "v-1")]
        };

        float[] vector = [0.1f, 0.2f, 0.3f, 0.4f];
        repo.VectorSearchAsync(vector, "vector_field", 5, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResult));

        var handler = new ElasticVectorSearchQueryHandler<TestProductDocument, string>(repo);
        var query = new ElasticVectorSearchQuery<TestProductDocument>(vector, "vector_field", TopK: 5);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Total.ShouldBe(1);
        result.Documents[0].Title.ShouldBe("Neural Net");
        await repo.Received(1).VectorSearchAsync(vector, "vector_field", 5, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticHybridSearchQueryHandler delegates to HybridSearchAsync")]
    public async Task ElasticHybridSearchQueryHandler_DelegatesToRepository()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var expectedResult = new KyrolusSearchResult<TestProductDocument> { Total = 2 };

        float[] vector = [0.5f, 0.6f];
        repo.HybridSearchAsync("AI", vector, "embedding", 10, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedResult));

        var handler = new ElasticHybridSearchQueryHandler<TestProductDocument, string>(repo);
        var query = new ElasticHybridSearchQuery<TestProductDocument>("AI", vector, "embedding", TopK: 10);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Total.ShouldBe(2);
        await repo.Received(1).HybridSearchAsync("AI", vector, "embedding", 10, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticCountQueryHandler delegates to CountAsync")]
    public async Task ElasticCountQueryHandler_DelegatesToRepository()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.CountAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult(42L));

        var handler = new ElasticCountQueryHandler<TestProductDocument, string>(repo);
        var count = await handler.Handle(new ElasticCountQuery<TestProductDocument>(), CancellationToken.None);

        count.ShouldBe(42L);
        await repo.Received(1).CountAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticGetByIdQueryHandler delegates to GetByIdAsync")]
    public async Task ElasticGetByIdQueryHandler_DelegatesToRepository()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var doc = new TestProductDocument { Id = "doc-99", Title = "Book" };
        repo.GetByIdAsync("doc-99", Arg.Any<CancellationToken>()).Returns(Task.FromResult<TestProductDocument?>(doc));

        var handler = new ElasticGetByIdQueryHandler<TestProductDocument, string>(repo);
        var result = await handler.Handle(new ElasticGetByIdQuery<TestProductDocument, string>("doc-99"), CancellationToken.None);

        result.ShouldNotBeNull();
        result.Id.ShouldBe("doc-99");
        result.Title.ShouldBe("Book");
    }

    [Fact(DisplayName = "ElasticIndexDocumentCommandHandler indexes document successfully")]
    public async Task ElasticIndexDocumentCommandHandler_IndexesDocument()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var doc = new TestProductDocument { Id = "doc-1", Title = "Tablet" };
        repo.AddAsync(doc, "doc-1", Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var handler = new ElasticIndexDocumentCommandHandler<TestProductDocument, string>(repo);
        var success = await handler.Handle(new ElasticIndexDocumentCommand<TestProductDocument, string>(doc, "doc-1"), CancellationToken.None);

        success.ShouldBeTrue();
        await repo.Received(1).AddAsync(doc, "doc-1", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticDeleteDocumentCommandHandler deletes document by Id")]
    public async Task ElasticDeleteDocumentCommandHandler_DeletesDocument()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.DeleteAsync("del-1", Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(true));

        var handler = new ElasticDeleteDocumentCommandHandler<TestProductDocument, string>(repo);
        var success = await handler.Handle(new ElasticDeleteDocumentCommand<TestProductDocument, string>("del-1"), CancellationToken.None);

        success.ShouldBeTrue();
        // ExpectedSeqNo/ExpectedPrimaryTerm are null on this command, so the repository call carries nulls too.
        await repo.Received(1).DeleteAsync("del-1", null, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "ElasticBulkIndexCommandHandler performs batch indexing")]
    public async Task ElasticBulkIndexCommandHandler_PerformsBatchIndex()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var expectedBulk = new KyrolusBulkResult { TotalCount = 2, IndexedCount = 2 };
        repo.BulkIndexAsync(Arg.Any<IEnumerable<(TestProductDocument Document, string Id)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedBulk));

        var handler = new ElasticBulkIndexCommandHandler<TestProductDocument, string>(repo);
        var items = new List<(TestProductDocument Document, string Id)>
        {
            (new TestProductDocument { Id = "1" }, "1"),
            (new TestProductDocument { Id = "2" }, "2")
        };

        var result = await handler.Handle(new ElasticBulkIndexCommand<TestProductDocument, string>(items), CancellationToken.None);

        result.IndexedCount.ShouldBe(2);
        // The handler now materializes command.Items into its own list (to count it for the Fix 3 batch-size
        // check) before forwarding it, so this asserts sequence equality rather than reference equality.
        await repo.Received(1).BulkIndexAsync(
            Arg.Is<IEnumerable<(TestProductDocument Document, string Id)>>(actual => actual.SequenceEqual(items)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regression (Fix 3 - unbounded bulk batch): ElasticBulkIndexCommandHandler rejects a batch exceeding KyrolusElasticBulkLimits.MaxBatchSize")]
    public async Task ElasticBulkIndexCommandHandler_ExceedsLimit_Throws()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var handler = new ElasticBulkIndexCommandHandler<TestProductDocument, string>(repo);
        var items = Enumerable.Range(0, KyrolusElasticBulkLimits.MaxBatchSize + 1)
            .Select(i => (new TestProductDocument { Id = i.ToString() }, i.ToString()))
            .ToList();

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new ElasticBulkIndexCommand<TestProductDocument, string>(items), CancellationToken.None));

        await repo.DidNotReceive().BulkIndexAsync(Arg.Any<IEnumerable<(TestProductDocument Document, string Id)>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regression (Fix 3 - unbounded bulk batch): ElasticBulkIndexCommandHandler accepts a batch exactly at KyrolusElasticBulkLimits.MaxBatchSize")]
    public async Task ElasticBulkIndexCommandHandler_AtLimit_Succeeds()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.BulkIndexAsync(Arg.Any<IEnumerable<(TestProductDocument Document, string Id)>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new KyrolusBulkResult { TotalCount = KyrolusElasticBulkLimits.MaxBatchSize, IndexedCount = KyrolusElasticBulkLimits.MaxBatchSize }));
        var handler = new ElasticBulkIndexCommandHandler<TestProductDocument, string>(repo);
        var items = Enumerable.Range(0, KyrolusElasticBulkLimits.MaxBatchSize)
            .Select(i => (new TestProductDocument { Id = i.ToString() }, i.ToString()))
            .ToList();

        var result = await handler.Handle(new ElasticBulkIndexCommand<TestProductDocument, string>(items), CancellationToken.None);

        result.IndexedCount.ShouldBe(KyrolusElasticBulkLimits.MaxBatchSize);
    }

    [Fact(DisplayName = "Regression (Fix 3 - unbounded bulk batch): ElasticBulkDeleteCommandHandler rejects a batch exceeding KyrolusElasticBulkLimits.MaxBatchSize")]
    public async Task ElasticBulkDeleteCommandHandler_ExceedsLimit_Throws()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var handler = new ElasticBulkDeleteCommandHandler<TestProductDocument, string>(repo);
        var ids = Enumerable.Range(0, KyrolusElasticBulkLimits.MaxBatchSize + 1).Select(i => i.ToString()).ToList();

        await Should.ThrowAsync<InvalidOperationException>(
            () => handler.Handle(new ElasticBulkDeleteCommand<TestProductDocument, string>(ids), CancellationToken.None));

        await repo.DidNotReceive().BulkDeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regression (Fix 3 - unbounded bulk batch): ElasticBulkDeleteCommandHandler accepts a batch exactly at KyrolusElasticBulkLimits.MaxBatchSize")]
    public async Task ElasticBulkDeleteCommandHandler_AtLimit_Succeeds()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.BulkDeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new KyrolusBulkResult { TotalCount = KyrolusElasticBulkLimits.MaxBatchSize, IndexedCount = KyrolusElasticBulkLimits.MaxBatchSize }));
        var handler = new ElasticBulkDeleteCommandHandler<TestProductDocument, string>(repo);
        var ids = Enumerable.Range(0, KyrolusElasticBulkLimits.MaxBatchSize).Select(i => i.ToString()).ToList();

        var result = await handler.Handle(new ElasticBulkDeleteCommand<TestProductDocument, string>(ids), CancellationToken.None);

        result.IndexedCount.ShouldBe(KyrolusElasticBulkLimits.MaxBatchSize);
    }

    [Fact(DisplayName = "ElasticBulkDeleteCommandHandler performs batch deletion")]
    public async Task ElasticBulkDeleteCommandHandler_PerformsBatchDelete()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        var expectedBulk = new KyrolusBulkResult { TotalCount = 3, IndexedCount = 3 };
        repo.BulkDeleteAsync(Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expectedBulk));

        var handler = new ElasticBulkDeleteCommandHandler<TestProductDocument, string>(repo);
        string[] ids = ["1", "2", "3"];

        var result = await handler.Handle(new ElasticBulkDeleteCommand<TestProductDocument, string>(ids), CancellationToken.None);

        result.IndexedCount.ShouldBe(3);
        // Same reasoning as the bulk-index test above: the handler now forwards its own materialized list.
        await repo.Received(1).BulkDeleteAsync(
            Arg.Is<IEnumerable<string>>(actual => actual.SequenceEqual(ids)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Regression (Fix 2 - mass assignment): ElasticUpdatePartialCommand implements IKyrolusPropertyUpdateRequest so the global allow-list behavior (PipelineOrder -940) can intercept it")]
    public void ElasticUpdatePartialCommand_ImplementsPropertyUpdateRequest()
    {
        // Root cause of the mass-assignment bug: ElasticUpdatePartialCommand<,> lets a caller (often built
        // directly from a PATCH request body) write ANY property that happens to exist on the target object,
        // with zero allow-listing - unlike the EF/Marten PatchCommand/ExecuteUpdateCommand equivalents, which
        // are guarded by IKyrolusPropertyUpdateRequest + the already-registered KyrolusPropertyAllowListBehavior
        // pipeline behavior. Without implementing the interface, that behavior's `request is IKyrolusPropertyUpdateRequest`
        // pattern match never matches this command, so it is never even inspected, regardless of what
        // AllowedProperties the caller thinks they configured.
        typeof(ElasticUpdatePartialCommand<,>).GetInterfaces().ShouldContain(typeof(IKyrolusPropertyUpdateRequest));
    }

    [Fact(DisplayName = "Regression (Fix 2 - mass assignment): with no AllowedProperties configured, ElasticUpdatePartialCommand stays fully unrestricted (backward compatible)")]
    public async Task ElasticUpdatePartialCommand_NoAllowListConfigured_RemainsUnrestricted()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<ElasticUpdatePartialCommand<TestProductDocument, string>, bool>();
        var command = new ElasticUpdatePartialCommand<TestProductDocument, string>("p-1", new { IsAdmin = true });

        var result = await behavior.Handle(command, _ => Task.FromResult(true), CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact(DisplayName = "Regression (Fix 2 - mass assignment): with AllowedProperties configured, a disallowed property on an anonymous-object PartialDocument is rejected")]
    public async Task ElasticUpdatePartialCommand_AllowListConfigured_RejectsDisallowedPropertyOnAnonymousObject()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<ElasticUpdatePartialCommand<TestProductDocument, string>, bool>();
        var command = new ElasticUpdatePartialCommand<TestProductDocument, string>("p-1", new { Title = "ok", IsAdmin = true })
        {
            AllowedProperties = new HashSet<string> { "Title" }
        };

        await Should.ThrowAsync<KyrolusSecurityException>(
            () => behavior.Handle(command, _ => Task.FromResult(true), CancellationToken.None));
    }

    [Fact(DisplayName = "Regression (Fix 2 - mass assignment): with AllowedProperties configured, a listed property on an anonymous-object PartialDocument passes through")]
    public async Task ElasticUpdatePartialCommand_AllowListConfigured_AllowsListedPropertyOnAnonymousObject()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<ElasticUpdatePartialCommand<TestProductDocument, string>, bool>();
        var command = new ElasticUpdatePartialCommand<TestProductDocument, string>("p-1", new { Title = "ok" })
        {
            AllowedProperties = new HashSet<string> { "Title" }
        };

        var result = await behavior.Handle(command, _ => Task.FromResult(true), CancellationToken.None);

        result.ShouldBeTrue();
    }

    [Fact(DisplayName = "Regression (Fix 2 - mass assignment): with AllowedProperties configured, a dictionary-shaped PartialDocument is checked by its KEYS, not the dictionary's own type properties")]
    public async Task ElasticUpdatePartialCommand_AllowListConfigured_ChecksDictionaryKeys()
    {
        var behavior = new KyrolusPropertyAllowListBehavior<ElasticUpdatePartialCommand<TestProductDocument, string>, bool>();
        var command = new ElasticUpdatePartialCommand<TestProductDocument, string>(
            "p-1",
            new Dictionary<string, object> { ["Price"] = 999m, ["IsAdmin"] = true })
        {
            AllowedProperties = new HashSet<string> { "Price" }
        };

        await Should.ThrowAsync<KyrolusSecurityException>(
            () => behavior.Handle(command, _ => Task.FromResult(true), CancellationToken.None));
    }

    [Fact(DisplayName = "ElasticUpdatePartialCommandHandler executes partial update")]
    public async Task ElasticUpdatePartialCommandHandler_ExecutesPartialUpdate()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.UpdatePartialAsync("p-1", Arg.Any<object>(), Arg.Any<long?>(), Arg.Any<long?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var handler = new ElasticUpdatePartialCommandHandler<TestProductDocument, string>(repo);
        var partial = new { Price = 1299m };

        var success = await handler.Handle(new ElasticUpdatePartialCommand<TestProductDocument, string>("p-1", partial), CancellationToken.None);

        success.ShouldBeTrue();
        // ExpectedSeqNo/ExpectedPrimaryTerm are null on this command, so the repository call carries nulls too.
        await repo.Received(1).UpdatePartialAsync("p-1", partial, null, null, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "KyrolusElasticReadModelProjector auto-extracts Id and syncs document")]
    public async Task KyrolusElasticReadModelProjector_SyncsDocument()
    {
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        repo.IndexName.Returns("test_products");
        repo.AddAsync(Arg.Any<TestProductDocument>(), "prod-100", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var projector = new KyrolusElasticReadModelProjector<TestProductDocument, string>(repo);
        var model = new TestProductDocument { Id = "prod-100", Title = "Monitor", Price = 350m };

        await projector.ProjectAsync(model, CancellationToken.None);

        await repo.Received(1).AddAsync(model, "prod-100", Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "AddKyrolusCqrsElasticsearch registers all queries, commands, and projectors")]
    public void AddKyrolusCqrsElasticsearch_RegistersAllServicesInDI()
    {
        var services = new ServiceCollection();
        var repo = Substitute.For<IKyrolusElasticRepository<TestProductDocument, string>>();
        services.AddSingleton(repo);

        services.AddKyrolusCqrsElasticsearch<TestProductDocument, string>();
        var sp = services.BuildServiceProvider();

        // Verify Queries
        sp.GetService<IKyrolusQueryHandler<ElasticSearchQuery<TestProductDocument>, KyrolusSearchResult<TestProductDocument>>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusQueryHandler<ElasticAutocompleteQuery<TestProductDocument>, IReadOnlyList<string>>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusQueryHandler<ElasticVectorSearchQuery<TestProductDocument>, KyrolusSearchResult<TestProductDocument>>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusQueryHandler<ElasticHybridSearchQuery<TestProductDocument>, KyrolusSearchResult<TestProductDocument>>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusQueryHandler<ElasticCountQuery<TestProductDocument>, long>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusQueryHandler<ElasticGetByIdQuery<TestProductDocument, string>, TestProductDocument?>>()
            .ShouldNotBeNull();

        // Verify Commands
        sp.GetService<IKyrolusCommandHandler<ElasticIndexDocumentCommand<TestProductDocument, string>, bool>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusCommandHandler<ElasticDeleteDocumentCommand<TestProductDocument, string>, bool>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusCommandHandler<ElasticBulkIndexCommand<TestProductDocument, string>, KyrolusBulkResult>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusCommandHandler<ElasticBulkDeleteCommand<TestProductDocument, string>, KyrolusBulkResult>>()
            .ShouldNotBeNull();
        sp.GetService<IKyrolusCommandHandler<ElasticUpdatePartialCommand<TestProductDocument, string>, bool>>()
            .ShouldNotBeNull();

        // Verify Read Model Projector
        sp.GetService<IKyrolusReadModelProjector
<TestProductDocument>>()
            .ShouldNotBeNull();
    }
}
