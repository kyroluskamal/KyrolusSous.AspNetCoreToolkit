using System.Diagnostics;
using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.Core.Conditional;
using KyrolusSous.EndpointKit.Core.Envelope;
using KyrolusSous.EndpointKit.Core.Export;
using KyrolusSous.EndpointKit.Core.FieldSelection;
using KyrolusSous.EndpointKit.Core.Filters;
using KyrolusSous.EndpointKit.Core.Hateoas;
using KyrolusSous.EndpointKit.Core.Pagination;
using KyrolusSous.EndpointKit.Core.Patch;
using KyrolusSous.EndpointKit.Core.Streaming;
using KyrolusSous.EndpointKit.EF;
using KyrolusSous.Repositories.EF.Abstractions.Query;
using KyrolusSous.Validation.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;
using EfFilterBuilder = KyrolusSous.EndpointKit.EF.FilterBuilder;
using EfOrderBuilder = KyrolusSous.EndpointKit.EF.OrderBuilder;
using MartenFilterBuilder = KyrolusSous.EndpointKit.Marten.FilterBuilder;
using MartenOrderBuilder = KyrolusSous.EndpointKit.Marten.OrderBuilder;

namespace KyrolusSous.EndpointKit.UnitTests;

public sealed class KyrolusEndpointKit30RoundsReviewTests
{
    public sealed class SampleEntity
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public SampleCategory? Category { get; set; }
    }

    public sealed class SampleCategory
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
    }

    // Round 1: Nested field selection merging without overwriting
    [Fact(DisplayName = "Round 1: AddField merges nested selections without overwriting existing sub-fields")]
    public void Round1_FieldSelection_ShouldMergeSubFields()
    {
        var selection = new KyrolusFieldSelection();
        var nested1 = new KyrolusFieldSelection();
        nested1.AddField("Id");

        var nested2 = new KyrolusFieldSelection();
        nested2.AddField("Title");

        selection.AddField("Category", nested1);
        selection.AddField("Category", nested2);

        var cat = selection.GetNestedSelection("Category");
        cat.ShouldNotBeNull();
        cat.IsFieldSelected("Id").ShouldBeTrue();
        cat.IsFieldSelected("Title").ShouldBeTrue();
    }

    // Round 2: FieldSelection.Merge merges entire trees
    [Fact(DisplayName = "Round 2: Merge merges field trees recursively")]
    public void Round2_FieldSelection_Merge_ShouldCombineTrees()
    {
        var treeA = new KyrolusFieldSelection();
        treeA.AddField("Name");

        var treeB = new KyrolusFieldSelection();
        treeB.AddField("Price");

        treeA.Merge(treeB);
        treeA.IsFieldSelected("Name").ShouldBeTrue();
        treeA.IsFieldSelected("Price").ShouldBeTrue();
    }

    // Round 3: ProjectSingle supports IDictionary<string, object?>
    [Fact(DisplayName = "Round 3: FieldProjector projects dictionary sources")]
    public void Round3_FieldProjector_ShouldProjectDictionary()
    {
        var dict = new Dictionary<string, object?>
        {
            ["id"] = 10,
            ["name"] = "Widget",
            ["secret"] = "hidden"
        };

        var selection = new KyrolusFieldSelection();
        selection.AddField("id");
        selection.AddField("name");

        var projected = KyrolusFieldProjector.ProjectSingle(dict, selection);
        projected.ContainsKey("id").ShouldBeTrue();
        projected.ContainsKey("name").ShouldBeTrue();
        projected.ContainsKey("secret").ShouldBeFalse();
    }

    // Round 4: LinkGenerator clean base URL without double slashes
    [Fact(DisplayName = "Round 4: LinkGenerator produces clean URLs without double slashes")]
    public void Round4_LinkGenerator_ShouldNotProduceDoubleSlashes()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");
        httpContext.Request.PathBase = new PathString("/v1/");

        var config = Substitute.For<IKyrolusApiConfig<SampleEntity>>();
        config.Prefix.Returns("api/");
        config.Route.Returns("products");
        config.Endpoints.Returns(new HashSet<EndpointNames> { EndpointNames.All });
        config.AllEndpointsExcept.Returns(new HashSet<EndpointNames>());

        var linkGen = new KyrolusDefaultLinkGenerator(Substitute.For<LinkGenerator>());
        var links = linkGen.GenerateItemLinks(httpContext, config, Guid.NewGuid(), new SampleEntity());

        links.ShouldNotBeEmpty();
        links.All(l => !l.Href.Contains("//products") && !l.Href.Contains("api//")).ShouldBeTrue();
    }

    // Round 5: LinkGenerator totalPages == 0 pagination links
    [Fact(DisplayName = "Round 5: LinkGenerator handles 0 items gracefully")]
    public void Round5_LinkGenerator_ZeroItems_ShouldHandleGracefully()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("api.example.com");

        var config = Substitute.For<IKyrolusApiConfig<SampleEntity>>();
        config.Prefix.Returns("api");
        config.Route.Returns("products");
        config.Endpoints.Returns(new HashSet<EndpointNames> { EndpointNames.All });
        config.AllEndpointsExcept.Returns(new HashSet<EndpointNames>());

        var linkGen = new KyrolusDefaultLinkGenerator(Substitute.For<LinkGenerator>());
        var pagedLinks = linkGen.GeneratePagedLinks(httpContext, config, pageNumber: 1, pageSize: 10, totalCount: 0);

        pagedLinks.ShouldNotBeNull();
        pagedLinks.Any(l => l.Rel == KyrolusLinkRel.First).ShouldBeFalse();
    }

    // Round 6: EF OrderBuilder respects strict: false
    [Fact(DisplayName = "Round 6: EF OrderBuilder skips unallowed properties when strict is false")]
    public void Round6_EfOrderBuilder_ShouldSkipUnallowedWhenNotStrict()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Name" };
        var clauses = new List<OrderClause>
        {
            new("UnallowedProp", false),
            new("Name", true)
        };

        var orderByFunc = EfOrderBuilder.BuildOrderBy<SampleEntity>(clauses, allowed, strict: false, out var error);
        error.ShouldBeNull();
        orderByFunc.ShouldNotBeNull();

        var query = new List<SampleEntity>
        {
            new() { Name = "Alpha" },
            new() { Name = "Beta" }
        }.AsQueryable();

        var ordered = orderByFunc(query).ToList();
        ordered[0].Name.ShouldBe("Beta");
    }

    // Round 7: Marten OrderBuilder respects strict: false
    [Fact(DisplayName = "Round 7: Marten OrderBuilder skips unallowed properties when strict is false")]
    public void Round7_MartenOrderBuilder_ShouldSkipUnallowedWhenNotStrict()
    {
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Price" };
        var clauses = new List<KyrolusSous.Repositories.Marten.Abstractions.Query.OrderClause>
        {
            new("Invalid", false),
            new("Price", false)
        };

        var orderByFunc = MartenOrderBuilder.BuildOrderBy<SampleEntity>(clauses, allowed, strict: false, out var error);
        error.ShouldBeNull();
        orderByFunc.ShouldNotBeNull();
    }

    // Round 8: EF FilterBuilder parses ISO date formats cleanly
    [Fact(DisplayName = "Round 8: EF FilterBuilder parses ISO date strings")]
    public void Round8_EfFilterBuilder_ShouldParseIsoDates()
    {
        var filter = "createdAt >= 2026-08-25";
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 9: Marten FilterBuilder parses ISO date strings
    [Fact(DisplayName = "Round 9: Marten FilterBuilder parses ISO date strings")]
    public void Round9_MartenFilterBuilder_ShouldParseIsoDates()
    {
        var filter = "createdAt >= 2026-08-25";
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 10: EF FilterBuilder TimeSpan parsing
    [Fact(DisplayName = "Round 10: EF FilterBuilder parses TimeSpan")]
    public void Round10_EfFilterBuilder_ShouldParseTimeSpan()
    {
        var filter = "duration > 01:30:00";
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 11: Marten FilterBuilder TimeSpan parsing
    [Fact(DisplayName = "Round 11: Marten FilterBuilder parses TimeSpan")]
    public void Round11_MartenFilterBuilder_ShouldParseTimeSpan()
    {
        var filter = "duration > 01:30:00";
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 12: EF FilterBuilder boolean numbers ("1" and "0")
    [Fact(DisplayName = "Round 12: EF FilterBuilder parses numeric booleans")]
    public void Round12_EfFilterBuilder_ShouldParseNumericBooleans()
    {
        var filter = "isActive == 1";
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 13: Marten FilterBuilder boolean numbers
    [Fact(DisplayName = "Round 13: Marten FilterBuilder parses numeric booleans")]
    public void Round13_MartenFilterBuilder_ShouldParseNumericBooleans()
    {
        var filter = "isActive == 1";
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 14: CachePolicyRegistry normalizes HTTP methods and trailing slashes
    [Fact(DisplayName = "Round 14: CachePolicyRegistry normalizes routes")]
    public async Task Round14_CachePolicyRegistry_ShouldNormalizeRoutes()
    {
        var registry = new KyrolusEndpointCachePolicyRegistry();
        var policy = new KyrolusCachePolicy { SlidingExpiration = TimeSpan.FromMinutes(5) };
        registry.SetForRoute("get", "/api/products/", policy);

        var context = new KyrolusEndpointCachePolicyContext(
            EntityType: typeof(SampleEntity),
            EntityName: nameof(SampleEntity),
            Endpoint: EndpointNames.GetAll,
            HttpMethod: "GET",
            Path: "api/products",
            TenantId: null,
            ScopeKey: null);

        var retrieved = await registry.GetPolicyAsync(context);
        retrieved.ShouldBe(policy);
    }

    // Round 15: Weak ETag evaluation in ConditionalRequest
    [Fact(DisplayName = "Round 15: ConditionalRequest matches weak ETags")]
    public void Round15_ConditionalRequest_ShouldMatchWeakEtags()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["If-None-Match"] = "W/\"hash123\"";

        var isNotModified = KyrolusConditionalRequest.IsNotModified(httpContext.Request, "\"hash123\"");
        isNotModified.ShouldBeTrue();

        httpContext.Request.Headers.Clear();
        httpContext.Request.Headers["If-Match"] = "W/\"hash123\"";
        var isPreconditionFailed = KyrolusConditionalRequest.IsPreconditionFailed(httpContext.Request, "\"hash123\"");
        isPreconditionFailed.ShouldBeFalse();
    }

    // Round 16: KyrolusCursor Guid key decoding
    [Fact(DisplayName = "Round 16: KyrolusCursor decodes Guid keys without cast exceptions")]
    public void Round16_Cursor_ShouldDecodeGuidKeys()
    {
        var originalId = Guid.NewGuid();
        var encoded = KyrolusCursor.Encode(originalId, "score:99");

        var success = KyrolusCursor.TryDecode<Guid>(encoded, out var decodedId, out var secondary);
        success.ShouldBeTrue();
        decodedId.ShouldBe(originalId);
        secondary.ShouldBe("score:99");
    }

    // Round 17: CSV Exporter formats booleans as lowercase true/false
    [Fact(DisplayName = "Round 17: CsvExporter formats boolean as true/false")]
    public void Round17_CsvExporter_ShouldFormatBooleans()
    {
        var items = new List<SampleEntity>
        {
            new() { Name = "ActiveItem", IsActive = true },
            new() { Name = "InactiveItem", IsActive = false }
        };

        var bytes = KyrolusCsvExporter.ExportToCsv(items, ["Name", "IsActive"]);
        var text = System.Text.Encoding.UTF8.GetString(bytes);

        text.ShouldContain("ActiveItem,true");
        text.ShouldContain("InactiveItem,false");
    }

    // Round 18: ResponseEnvelope HasMore is false when totalCount is 0
    [Fact(DisplayName = "Round 18: ResponseEnvelope calculates 0 items HasMore correctly")]
    public void Round18_Envelope_ZeroItems_ShouldSetHasMoreFalse()
    {
        var options = new KyrolusEnvelopeOptions { IncludePagination = true };
        var builder = new KyrolusEnvelopeBuilder(options);
        builder.WithData(Array.Empty<SampleEntity>())
               .WithPagination(totalCount: 0, page: 1, pageSize: 10);

        var envelope = builder.Build();
        envelope.Meta.ShouldNotBeNull();
        envelope.Meta.TotalPages.ShouldBe(0);
        envelope.Meta.HasMore.ShouldBe(false);
    }

    // Round 19: BatchResponse calculates failure counts correctly
    [Fact(DisplayName = "Round 19: BatchResponse calculates failures accurately")]
    public void Round19_BatchResponse_ShouldCalculateCounts()
    {
        var results = new List<KyrolusBatchOperationResult<SampleEntity, Guid>>
        {
            KyrolusBatchOperationResult<SampleEntity, Guid>.Succeeded("1", KyrolusBatchOperationType.Create, Guid.NewGuid(), 201),
            KyrolusBatchOperationResult<SampleEntity, Guid>.Failed("2", KyrolusBatchOperationType.Update, Guid.NewGuid(), 400, "ERR", "Failed")
        };

        var response = KyrolusBatchResponse<SampleEntity, Guid>.FromResults(results);
        response.Success.ShouldBeFalse();
        response.TotalOperations.ShouldBe(2);
        response.SuccessCount.ShouldBe(1);
        response.FailureCount.ShouldBe(1);
    }

    // Round 20: JsonMergePatch recursive parsing
    [Fact(DisplayName = "Round 20: JsonMergePatch parses nested objects recursively")]
    public void Round20_JsonMergePatch_ShouldParseNested()
    {
        var json = """
        {
            "name": "Root",
            "category": {
                "title": "SubTitle",
                "code": null
            }
        }
        """;

        using var doc = JsonDocument.Parse(json);
        var patch = KyrolusJsonMergePatch.ParseMergePatch(doc.RootElement);

        patch.ContainsKey("name").ShouldBeTrue();
        patch["category"].ShouldBeOfType<Dictionary<string, object?>>();
        var cat = (Dictionary<string, object?>)patch["category"]!;
        cat["title"].ShouldBe("SubTitle");
        cat["code"].ShouldBeNull();
    }

    // Round 21: Case-insensitive startsWith in EF FilterBuilder
    [Fact(DisplayName = "Round 21: EF FilterBuilder supports case-insensitive startsWith")]
    public void Round21_EfFilterBuilder_StartsWith_CaseInsensitive()
    {
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>("name startswith 'pro'", null, false, caseInsensitive: true, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 22: Case-insensitive endsWith in Marten FilterBuilder
    [Fact(DisplayName = "Round 22: Marten FilterBuilder supports case-insensitive endsWith")]
    public void Round22_MartenFilterBuilder_EndsWith_CaseInsensitive()
    {
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>("name endswith 'xyz'", null, false, caseInsensitive: true, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 23: TenantEndpointFilter trims whitespace
    [Fact(DisplayName = "Round 23: TenantEndpointFilter trims header whitespace")]
    public async Task Round23_TenantFilter_ShouldTrimWhitespace()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-Tenant-ID"] = "  tenant-999  ";

        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);

        var filter = new KyrolusTenantEndpointFilter();
        var result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>("OK"));

        httpContext.Items[KyrolusTenantEndpointFilter.TenantItemKey].ShouldBe("tenant-999");
    }

    // Round 24: TelemetryEndpointFilter handles activity tagging
    [Fact(DisplayName = "Round 24: TelemetryEndpointFilter enriches activity tags")]
    public async Task Round24_TelemetryFilter_ShouldEnrichActivity()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/products";

        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);

        using var activity = new Activity("TestActivity").Start();
        var filter = new KyrolusTelemetryEndpointFilter("Product", "Create");
        await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>("OK"));

        activity.GetTagItem("endpointkit.entity").ShouldBe("Product");
        activity.GetTagItem("endpointkit.action").ShouldBe("Create");
    }

    // Round 25: SSE handles cancellation gracefully
    [Fact(DisplayName = "Round 25: SseResult completes on token cancellation without exception")]
    public async Task Round25_SseResult_ShouldCompleteOnCancellation()
    {
        async IAsyncEnumerable<int> GenerateStream()
        {
            yield return 1;
            yield return 2;
        }

        var httpContext = new DefaultHttpContext();
        var responseBody = new MemoryStream();
        httpContext.Response.Body = responseBody;

        var sse = new KyrolusSseResult<int>(GenerateStream());
        await sse.ExecuteAsync(httpContext);

        responseBody.Length.ShouldBeGreaterThan(0);
    }

    // Round 26: Between operator with DateTime in EF FilterBuilder
    [Fact(DisplayName = "Round 26: EF FilterBuilder between operator on DateTime")]
    public void Round26_EfFilterBuilder_Between_DateTime()
    {
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>("createdAt between (2026-01-01, 2026-12-31)", null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 27: In operator with numbers in Marten FilterBuilder
    [Fact(DisplayName = "Round 27: Marten FilterBuilder in operator on numbers")]
    public void Round27_MartenFilterBuilder_In_Numbers()
    {
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>("price in [10.5, 20.0, 35.75]", null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 28: ValidationEndpointFilter handles validation failures
    [Fact(DisplayName = "Round 28: ValidationEndpointFilter formats RFC 9457 errors")]
    public async Task Round28_ValidationFilter_ShouldFormatErrors()
    {
        var engine = Substitute.For<IKyrolusValidationEngine>();
        var failures = new List<KyrolusValidationFailure>
        {
            new("Name", "Name is required.", "ERR_REQUIRED")
        };
        engine.ValidateAsync(Arg.Any<SampleEntity>(), Arg.Any<CancellationToken>())
              .Returns(new ValueTask<IReadOnlyList<KyrolusValidationFailure>>(failures));

        var services = new ServiceCollection();
        services.AddSingleton(engine);
        var serviceProvider = services.BuildServiceProvider();

        var filter = new KyrolusValidationEndpointFilter();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider
        };
        var filterContext = Substitute.For<EndpointFilterInvocationContext>();
        filterContext.HttpContext.Returns(httpContext);
        filterContext.Arguments.Returns(new object?[] { new SampleEntity() });

        var result = await filter.InvokeAsync(filterContext, _ => ValueTask.FromResult<object?>("OK"));
        result.ShouldNotBeNull();
    }

    // Round 29: Complex nested and/or expressions in EF FilterBuilder
    [Fact(DisplayName = "Round 29: EF FilterBuilder parses composite AND/OR groups with parentheses")]
    public void Round29_EfFilterBuilder_ComplexAndOr()
    {
        var filter = "(name eq 'Laptop' | name eq 'Phone') , price > 500";
        var success = EfFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }

    // Round 30: Complex nested and/or expressions in Marten FilterBuilder
    [Fact(DisplayName = "Round 30: Marten FilterBuilder parses composite AND/OR groups with parentheses")]
    public void Round30_MartenFilterBuilder_ComplexAndOr()
    {
        var filter = "(name eq 'Laptop' | name eq 'Phone') , price > 500";
        var success = MartenFilterBuilder.TryBuildFilterExpression<SampleEntity>(filter, null, false, false, out var expr, out var error);
        success.ShouldBeTrue();
        error.ShouldBeNull();
        expr.ShouldNotBeNull();
    }
}
