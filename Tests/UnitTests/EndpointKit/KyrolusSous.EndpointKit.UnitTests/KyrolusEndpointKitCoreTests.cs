using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Core.Envelope;
using KyrolusSous.EndpointKit.Core.FieldSelection;
using KyrolusSous.EndpointKit.Core.Hateoas;
using Shouldly;
using System.Text.Json;
using Xunit;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKitCoreTests
{
    public sealed class TestProduct
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "Envelope: Response<T> creates Success and Failure correctly")]
    public void Response_Should_Create_Success_And_Failure()
    {
        var success = Response<string>.Success("SampleData", "Loaded successfully", 200);
        success.IsSuccess.ShouldBeTrue();
        success.StatusCode.ShouldBe(200);
        success.Data.ShouldBe("SampleData");
        success.Message.ShouldBe("Loaded successfully");

        var failure = Response<string>.Failure("Invalid argument", 400);
        failure.IsSuccess.ShouldBeFalse();
        failure.StatusCode.ShouldBe(400);
        failure.Data.ShouldBeNull();
        failure.Message.ShouldBe("Invalid argument");
    }

    [Fact(DisplayName = "Envelope: KyrolusResponseEnvelope builds full metadata and pagination")]
    public void KyrolusResponseEnvelope_Should_Build_Metadata_And_Pagination()
    {
        var options = new KyrolusEnvelopeOptions
        {
            IncludeMeta = true,
            IncludePagination = true,
            IncludeTimestamp = true,
            IncludeTraceId = true,
            IncludeVersion = true
        };

        var builder = new KyrolusEnvelopeBuilder(options);
        var envelope = builder
            .WithData(new { Title = "Test" })
            .WithStatusCode(200)
            .WithTraceId("trace-123")
            .WithVersion("v1.0")
            .WithPagination(totalCount: 100, page: 2, pageSize: 20)
            .Build();

        envelope.Success.ShouldBeTrue();
        envelope.Data.ShouldNotBeNull();
        envelope.Meta.ShouldNotBeNull();
        envelope.Meta.Status.ShouldBe(200);
        envelope.Meta.TraceId.ShouldBe("trace-123");
        envelope.Meta.Version.ShouldBe("v1.0");
        envelope.Meta.TotalCount.ShouldBe(100);
        envelope.Meta.Page.ShouldBe(2);
        envelope.Meta.PageSize.ShouldBe(20);
        envelope.Meta.TotalPages.ShouldBe(5);
        envelope.Meta.HasMore.ShouldBe(true);
    }

    [Fact(DisplayName = "Envelope: KyrolusResponseEnvelope builds error envelope with details")]
    public void KyrolusResponseEnvelope_Should_Build_Error()
    {
        var options = new KyrolusEnvelopeOptions();
        var builder = new KyrolusEnvelopeBuilder(options);

        var details = new List<KyrolusErrorDetail>
        {
            new("Email", "ERR_INVALID_EMAIL", "Email format is invalid")
        };

        var envelope = builder
            .WithStatusCode(422)
            .WithError("VALIDATION_ERROR", "Validation failed", details)
            .Build();

        envelope.Success.ShouldBeFalse();
        envelope.Error.ShouldNotBeNull();
        envelope.Error.Code.ShouldBe("VALIDATION_ERROR");
        envelope.Error.Message.ShouldBe("Validation failed");
        envelope.Error.Details.ShouldNotBeNull();
        envelope.Error.Details.Count.ShouldBe(1);
        envelope.Error.Details[0].Field.ShouldBe("Email");
    }

    [Fact(DisplayName = "FieldSelection: SelectFields filters object properties properly")]
    public void FieldSelection_Should_Filter_Object_Properties()
    {
        var product = new TestProduct
        {
            Id = 1,
            Name = "Laptop",
            Price = 999.99m,
            Category = "Electronics"
        };

        var success = KyrolusFieldSelectionParser.TryParse("Id,Name", out var selection, out var error);
        success.ShouldBeTrue();
        selection.ShouldNotBeNull();

        var filtered = KyrolusFieldProjector.Project(product, selection);
        filtered.ShouldNotBeNull();

        var dict = filtered as IDictionary<string, object?>;
        dict.ShouldNotBeNull();
        dict.ContainsKey("Id").ShouldBeTrue();
        dict.ContainsKey("Name").ShouldBeTrue();
        dict.ContainsKey("Price").ShouldBeFalse();
        dict.ContainsKey("Category").ShouldBeFalse();
    }

    [Fact(DisplayName = "FieldSelection: SelectFields filters collection items properly")]
    public void FieldSelection_Should_Filter_Collection()
    {
        var products = new List<TestProduct>
        {
            new() { Id = 1, Name = "Laptop", Price = 999.99m, Category = "Electronics" },
            new() { Id = 2, Name = "Mouse", Price = 25.00m, Category = "Accessories" }
        };

        var success = KyrolusFieldSelectionParser.TryParse("Id,Price", out var selection, out var error);
        success.ShouldBeTrue();
        selection.ShouldNotBeNull();

        var filtered = KyrolusFieldProjector.Project(products, selection);
        filtered.ShouldNotBeNull();

        var list = (filtered as IEnumerable<Dictionary<string, object?>>)?.ToList();
        list.ShouldNotBeNull();
        list.Count.ShouldBe(2);
        list[0].ContainsKey("Id").ShouldBeTrue();
        list[0].ContainsKey("Price").ShouldBeTrue();
        list[0].ContainsKey("Category").ShouldBeFalse();
    }

    [Fact(DisplayName = "HATEOAS: KyrolusLinkGenerator creates appropriate REST links")]
    public void Hateoas_Should_Generate_Valid_Links()
    {
        var links = new List<KyrolusLink>
        {
            KyrolusLink.Self("/api/products/42"),
            KyrolusLink.Edit("/api/products/42"),
            KyrolusLink.Delete("/api/products/42")
        };

        links.Count.ShouldBe(3);
        links[0].Href.ShouldBe("/api/products/42");
        links[0].Rel.ShouldBe("self");
        links[0].Method.ShouldBe("GET");
        links[1].Href.ShouldBe("/api/products/42");
        links[1].Rel.ShouldBe("edit");
        links[1].Method.ShouldBe("PUT");
    }

    [Fact(DisplayName = "Batch: KyrolusBatchRequest deserializes operations properly")]
    public void BatchModels_Should_Serialize_And_Deserialize()
    {
        var batch = new KyrolusBatchRequest<TestProduct, int>
        {
            Atomic = true,
            ContinueOnError = false,
            Operations =
            [
                new() { Operation = KyrolusBatchOperationType.Create, Data = new TestProduct { Name = "Keyboard" } },
                new() { Operation = KyrolusBatchOperationType.Delete, Id = 1 }
            ]
        };

        batch.Operations.Count.ShouldBe(2);
        batch.Atomic.ShouldBeTrue();
        batch.ContinueOnError.ShouldBeFalse();
        batch.Operations[0].Operation.ShouldBe(KyrolusBatchOperationType.Create);
        batch.Operations[1].Operation.ShouldBe(KyrolusBatchOperationType.Delete);
    }

    [Fact(DisplayName = "OutputCache: Policy registry resolves default and entity-specific policies")]
    public async Task PolicyRegistry_Should_Resolve_Configured_Policies()
    {
        var registry = new KyrolusEndpointCachePolicyRegistry();
        var defaultPolicy = new Caching.Abstractions.KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(10));

        var productPolicy = new Caching.Abstractions.KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(30));

        registry.SetDefault(defaultPolicy);
        registry.SetForEntity<TestProduct>(productPolicy);

        var ctxProduct = new KyrolusEndpointCachePolicyContext(
            typeof(TestProduct),
            "TestProduct",
            EndpointNames.GetAll,
            "GET",
            "/api/products",
            null,
            null);

        var resolvedProduct = await registry.GetPolicyAsync(ctxProduct);
        resolvedProduct.ShouldNotBeNull();
        resolvedProduct.AbsoluteExpirationRelativeToNow.ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact(DisplayName = "Envelope: KyrolusResponseEnvelope serializes and deserializes properly")]
    public void Envelope_Should_Roundtrip_Json()
    {
        var envelope = KyrolusResponseEnvelope.Ok(
            new { Id = 1, Name = "Test" },
            new KyrolusResponseMeta { Status = 200, TraceId = "t-123" },
            [KyrolusLink.Self("/api/items/1")]);

        var json = JsonSerializer.Serialize(envelope);
        json.ShouldContain("\"success\":true");
        json.ShouldContain("\"status\":200");
        json.ShouldContain("\"_links\"");

        var deserialized = JsonSerializer.Deserialize<KyrolusResponseEnvelope>(json);
        deserialized.ShouldNotBeNull();
        deserialized.Success.ShouldBeTrue();
        deserialized.Meta.ShouldNotBeNull();
        deserialized.Meta.Status.ShouldBe(200);
    }
}
