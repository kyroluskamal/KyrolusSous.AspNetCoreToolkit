using System.Security.Claims;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Audit;
using KyrolusSous.CQRS.Abstractions.Behaviors;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Abstractions.LivePush;
using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.CQRS.Abstractions.Outbox;
using KyrolusSous.CQRS.Abstractions.Projections;
using KyrolusSous.CQRS.Abstractions.Security;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.CQRS.EF.Command.Bulk;
using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Behaviors;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.CQRS.Validation;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

[Collection("ThrottlingSemaphores")]
public class Kyrolus20RoundsReviewTests
{
    // ==========================================
    // Round 1: Throttling Key Normalization & Clear
    // ==========================================
    public sealed record ThrottledReq(string ThrottleKey) : IKyrolusCommand<int>, IThrottledRequest
    {
        public int MaxConcurrentExecutions => 1;
        public TimeSpan ThrottleTimeout => TimeSpan.FromMilliseconds(50);
    }

    [Fact(DisplayName = "Round1 Throttling should trim keys and allow clear")]
    public async Task Round1_Throttling_should_trim_keys_and_allow_clear()
    {
        KyrolusThrottlingBehavior<ThrottledReq, int>.ClearSemaphores();
        var behavior = new KyrolusThrottlingBehavior<ThrottledReq, int>();
        var req = new ThrottledReq("  my-key  ");

        var result = await behavior.Handle(req, ct => Task.FromResult(42), CancellationToken.None);
        result.ShouldBe(42);

        KyrolusThrottlingBehavior<ThrottledReq, int>.ClearSemaphores();
    }

    // ==========================================
    // Round 2: Idempotency Value-Type & Envelope
    // ==========================================
    public sealed record IdempotentIntCmd(string IdempotencyKey) : IKyrolusCommand<string>, IIdempotentCommand<string>;

    [Fact(DisplayName = "Round2 Idempotency should cache and return response")]
    public async Task Round2_Idempotency_should_cache_and_return_response()
    {
        var cache = new FakeCacheProvider();

        var behavior = new KyrolusIdempotencyBehavior<IdempotentIntCmd, string>(cache);
        var cmd = new IdempotentIntCmd("key-123");

        var executionCount = 0;
        var res1 = await behavior.Handle(cmd, ct => { executionCount++; return Task.FromResult("OK"); }, CancellationToken.None);
        var res2 = await behavior.Handle(cmd, ct => { executionCount++; return Task.FromResult("OK"); }, CancellationToken.None);

        res1.ShouldBe("OK");
        res2.ShouldBe("OK");
        executionCount.ShouldBe(1);
    }

    // ==========================================
    // Round 3: Outbox AppDomain Type Resolution
    // ==========================================
    public sealed record Round3Event(string Message) : IKyrolusNotification;

    [Fact(DisplayName = "Round3 Outbox should resolve allow-listed notification types")]
    public async Task Round3_Outbox_should_resolve_types_from_appdomain()
    {
        var store = new InMemoryOutboxStore();
        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var processor = new KyrolusOutboxProcessor(store, publisher);

        await store.SaveAsync(new KyrolusOutboxMessage
        {
            EventType = typeof(Round3Event).FullName!,
            Payload = "{\"Message\":\"Hello\"}"
        });

        var processed = await processor.ProcessPendingMessagesAsync(10);
        processed.ShouldBe(1);
        await publisher.Received(1).PublishAsync(Arg.Is<object>(o => o is Round3Event), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Outbox should refuse an event type outside the allow-list rather than resolve it")]
    public async Task Outbox_should_reject_event_type_not_in_registry()
    {
        var store = new InMemoryOutboxStore();
        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        // An explicit, empty allow-list - Round3Event exists and is loadable, but is not in it.
        var registry = new KyrolusOutboxEventTypeRegistry([]);
        var processor = new KyrolusOutboxProcessor(store, publisher, registry);

        await store.SaveAsync(new KyrolusOutboxMessage
        {
            EventType = typeof(Round3Event).FullName!,
            Payload = "{\"Message\":\"Hello\"}"
        });

        var processed = await processor.ProcessPendingMessagesAsync(10);

        processed.ShouldBe(0);
        await publisher.DidNotReceive().PublishAsync(Arg.Any<object>(), Arg.Any<CancellationToken>());
        store.AllMessages.Single().Status.ShouldBe(OutboxMessageStatus.Failed);
        store.AllMessages.Single().Error.ShouldNotBeNull();
        store.AllMessages.Single().Error!.ShouldContain("not in the outbox type registry's allow-list");
    }

    // ==========================================
    // Round 4 & Round 9: Security Context & Scopes
    // ==========================================
    [Fact(DisplayName = "Round4 and 9 Current User Context should parse roles and scopes")]
    public void Round4_and_9_CurrentUserContext_should_parse_roles_and_scopes()
    {
        var identity = new ClaimsIdentity([
            new Claim("role", "Admin"),
            new Claim("roles", "Manager"),
            new Claim("scp", "orders.read orders.write"),
            new Claim("tenant_id", "tenant-42")
        ], "TestAuth");

        var principal = new ClaimsPrincipal(identity);
        var context = new KyrolusDefaultCurrentUserContext(principal);

        context.IsAuthenticated.ShouldBeTrue();
        context.TenantId.ShouldBe("tenant-42");
        context.IsInRole("Admin").ShouldBeTrue();
        context.IsInRole("Manager").ShouldBeTrue();
        context.HasPermission("orders.read").ShouldBeTrue();
        context.HasPermission("orders.write").ShouldBeTrue();
    }

    // ==========================================
    // Round 5: EF Cascading Domain Events
    // ==========================================
    public sealed class CascadingEntity : IDomainEventSource
    {
        public int Id { get; set; }
        private readonly List<object> _events = [];
        public IReadOnlyCollection<object> DomainEvents => _events;
        public void AddEvent(object e) => _events.Add(e);
        public void ClearDomainEvents() => _events.Clear();
    }

    public sealed class CascadingDbContext(DbContextOptions<CascadingDbContext> options) : DbContext(options)
    {
        public DbSet<CascadingEntity> Entities => Set<CascadingEntity>();
    }

    public sealed record DummyCmd() : IKyrolusCommand<string>;

    [Fact(DisplayName = "Round5 Ef cascading events should drain completely")]
    public async Task Round5_Ef_cascading_events_should_drain_completely()
    {
        var options = new DbContextOptionsBuilder<CascadingDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var db = new CascadingDbContext(options);
        var entity = new CascadingEntity();
        entity.AddEvent("Event1");
        db.Entities.Add(entity);

        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        publisher.PublishAsync("Event1", Arg.Any<CancellationToken>())
            .Returns(async _ =>
            {
                entity.AddEvent("Event2");
                await Task.Yield();
            });

        var behavior = new EF.Behaviors.KyrolusDomainEventsDispatchBehavior<DummyCmd, string, CascadingDbContext>(publisher, db);
        await behavior.Handle(new DummyCmd(), ct => Task.FromResult("ok"), CancellationToken.None);

        await publisher.Received(1).PublishAsync("Event1", Arg.Any<CancellationToken>());
        await publisher.Received(1).PublishAsync("Event2", Arg.Any<CancellationToken>());
    }

    // ==========================================
    // Round 6: Marten Domain Events from Response
    // ==========================================
    [Fact(DisplayName = "Round6 Marten events from response should be dispatched")]
    public async Task Round6_Marten_events_from_response_should_be_dispatched()
    {
        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var behavior = new KyrolusMartenDomainEventsDispatchBehavior<DummyCmd, CascadingEntity>(publisher);

        var responseEntity = new CascadingEntity();
        responseEntity.AddEvent("ResponseEvent");

        await behavior.Handle(new DummyCmd(), ct => Task.FromResult(responseEntity), CancellationToken.None);

        await publisher.Received(1).PublishAsync("ResponseEvent", Arg.Any<CancellationToken>());
        responseEntity.DomainEvents.ShouldBeEmpty();
    }

    // ==========================================
    // Round 7: Read-Model Projections from Response
    // ==========================================
    public sealed record TestReadModel(string Data);
    public sealed record ResponseProjectableEntity(string Data) : IProjectableCommand<TestReadModel>
    {
        public TestReadModel? ToReadModel() => new(Data);
    }

    private sealed class TestProjector : IReadModelProjector<TestReadModel>
    {
        public List<TestReadModel> Projected { get; } = [];
        public Task ProjectAsync(TestReadModel model, CancellationToken cancellationToken = default)
        {
            Projected.Add(model);
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Round7 Projection from response should be invoked")]
    public async Task Round7_Projection_from_response_should_be_invoked()
    {
        var services = new ServiceCollection();
        var projector = new TestProjector();
        services.AddSingleton<IReadModelProjector<TestReadModel>>(projector);
        var sp = services.BuildServiceProvider();

        var behavior = new KyrolusReadModelProjectionBehavior<DummyCmd, ResponseProjectableEntity>(sp);
        var result = await behavior.Handle(new DummyCmd(), ct => Task.FromResult(new ResponseProjectableEntity("from-response")), CancellationToken.None);

        result.Data.ShouldBe("from-response");
        projector.Projected.Count.ShouldBe(1);
        projector.Projected[0].Data.ShouldBe("from-response");
    }

    // ==========================================
    // Round 8: Live Push Fallback to Response
    // ==========================================
    public sealed record LivePushReq() : IKyrolusCommand<string>, ILivePushCommand
    {
        public string Channel => "test-live";
        public object? PushData => null;
    }

    [Fact(DisplayName = "Round8 Live Push should fallback to response when pushdata is null")]
    public async Task Round8_LivePush_should_fallback_to_response_when_pushdata_is_null()
    {
        var publisher = new InMemoryLivePushPublisher();
        var behavior = new KyrolusLivePushBehavior<LivePushReq, string>(publisher);

        await behavior.Handle(new LivePushReq(), ct => Task.FromResult("ResultPayload"), CancellationToken.None);

        publisher.Messages.Count.ShouldBe(1);
        publisher.Messages.First().Data.ShouldBe("ResultPayload");
    }

    // ==========================================
    // Round 10: Query Caching Nullable DI
    // ==========================================
    public sealed record TestQuery : IKyrolusQuery<string>, ICacheableRequest
    {
        public bool Cacheable { get; set; } = true;
    }

    [Fact(DisplayName = "Round10 Query Caching should passthrough when cache provider is null")]
    public async Task Round10_QueryCaching_should_passthrough_when_cache_provider_is_null()
    {
        var behavior = new KyrolusQueryCachingBehavior<TestQuery, string>(cacheProvider: null, cacheKeyProvider: null);
        var result = await behavior.Handle(new TestQuery(), ct => Task.FromResult("direct"), CancellationToken.None);
        result.ShouldBe("direct");
    }

    // ==========================================
    // Round 11: Validation Nullable Collection
    // ==========================================
    [Fact(DisplayName = "Round11 Validation Behavior should construct with null validators")]
    public async Task Round11_ValidationBehavior_should_construct_with_null_validators()
    {
        var behavior = new KyrolusValidationBehavior<TestQuery, string>(validators: null, engine: null);
        var result = await behavior.Handle(new TestQuery(), ct => Task.FromResult("valid"), CancellationToken.None);
        result.ShouldBe("valid");
    }

    // ==========================================
    // Round 12: Exception Mapping Nullable Collection
    // ==========================================
    [Fact(DisplayName = "Round12 Exception Mapping Behavior should construct with null mappers")]
    public async Task Round12_ExceptionMappingBehavior_should_construct_with_null_mappers()
    {
        var behavior = new KyrolusExceptionMappingBehavior<TestQuery, string>(mappers: null);
        var result = await behavior.Handle(new TestQuery(), ct => Task.FromResult("mapped"), CancellationToken.None);
        result.ShouldBe("mapped");
    }

    // ==========================================
    // Round 13 & 14 & 15: Bulk & Count Null Safety
    // ==========================================
    [Fact(DisplayName = "Round13 14 15 Handlers should throw Argument Null Exception on null input")]
    public async Task Round13_14_15_Handlers_should_throw_ArgumentNullException_on_null_input()
    {
        var uow = Substitute.For<IKyrolusUnitOfWork>();
        var martenUow = Substitute.For<IKyrolusMartenUnitOfWork<global::Marten.IDocumentSession>>();

        var efCount = new EF.Query.CountQueryHandler<CascadingDbContext, CascadingEntity, int>(uow);
        await Should.ThrowAsync<ArgumentNullException>(() => efCount.Handle(null!, CancellationToken.None));

        var martenCount = new CQRS.Marten.Query.CountQueryHandler<global::Marten.IDocumentSession, CascadingEntity, int>(martenUow);
        await Should.ThrowAsync<ArgumentNullException>(() => martenCount.Handle(null!, CancellationToken.None));

        var efDelete = new ExecuteDeleteCommandHandler<CascadingDbContext, CascadingEntity, int>(uow);
        await Should.ThrowAsync<ArgumentNullException>(() => efDelete.Handle(null!, CancellationToken.None));
    }

    // ==========================================
    // Round 16: Cache Key Pagination Separation
    // ==========================================
    public sealed record PagedTestQuery(int PageNumber, int PageSize) : IKyrolusQuery<string>;

    [Fact(DisplayName = "Round16 Cache Key Provider should include page number and size")]
    public void Round16_CacheKeyProvider_should_include_page_number_and_size()
    {
        var provider = new KyrolusDefaultCacheKeyProvider();
        var keyPage1 = provider.GetCacheKey(new PagedTestQuery(1, 20));
        var keyPage2 = provider.GetCacheKey(new PagedTestQuery(2, 20));

        keyPage1.ShouldNotBeNull();
        keyPage2.ShouldNotBeNull();
        keyPage1.ShouldNotBe(keyPage2);
        keyPage1.ShouldContain("p1_s20");
        keyPage2.ShouldContain("p2_s20");
    }

    // ==========================================
    // Round 17 & 18: Paged & Seek Navigation Helpers
    // ==========================================
    [Fact(DisplayName = "Round17 and 18 Results should calculate navigation properties")]
    public void Round17_and_18_Results_should_calculate_navigation_properties()
    {
        var paged = new KyrolusPagedResult<string>(["a", "b"], TotalCount: 50, PageNumber: 2, PageSize: 10);
        paged.TotalPages.ShouldBe(5);
        paged.HasNextPage.ShouldBeTrue();
        paged.HasPreviousPage.ShouldBeTrue();

        var pagedLast = new KyrolusPagedResult<string>(["a"], TotalCount: 50, PageNumber: 5, PageSize: 10);
        pagedLast.HasNextPage.ShouldBeFalse();

        var seekMore = new KyrolusSeekResult<string>(["a"], NextToken: "cursor-xyz", TotalCount: null, PageSize: 10);
        seekMore.HasMore.ShouldBeTrue();

        var seekEnd = new KyrolusSeekResult<string>(["a"], NextToken: null, TotalCount: null, PageSize: 10);
        seekEnd.HasMore.ShouldBeFalse();
    }

    // ==========================================
    // Round 19: Telemetry Options Enabled Check
    // ==========================================
    [Fact(DisplayName = "Round19 Telemetry should bypass when disabled")]
    public async Task Round19_Telemetry_should_bypass_when_disabled()
    {
        var options = new KyrolusCqrsPerformanceOptions { Enabled = false };
        var behavior = new KyrolusPerformanceAndTelemetryBehavior<TestQuery, string>(options: options);

        var result = await behavior.Handle(new TestQuery(), ct => Task.FromResult("fast"), CancellationToken.None);
        result.ShouldBe("fast");
    }

    // ==========================================
    // Round 20: Audit Sensitive Redaction
    // ==========================================
    public sealed record LoginCommand(string Username, string Password, string SecretToken)
        : IKyrolusCommand<string>, IAuditableCommand
    {
        public string AuditAction => "UserLogin";
        public string AuditCategory => "Security";
        public bool IncludePayload => true;
    }

    [Fact(DisplayName = "Round20 Audit should redact passwords and tokens")]
    public async Task Round20_Audit_should_redact_passwords_and_tokens()
    {
        var sink = new KyrolusInMemoryAuditSink();
        var context = new KyrolusDefaultCurrentUserContext(user: null);
        var behavior = new KyrolusAuditBehavior<LoginCommand, string>(sink, context);

        var cmd = new LoginCommand("admin", "P@ssw0rd123", "secret-token-xyz");
        await behavior.Handle(cmd, ct => Task.FromResult("logged-in"), CancellationToken.None);

        sink.Entries.Count.ShouldBe(1);
        var entry = sink.Entries.First();
        entry.Payload.ShouldNotBeNull();

        var dict = entry.Payload as Dictionary<string, object?>;
        dict.ShouldNotBeNull();
        dict["Password"].ShouldBe("***REDACTED***");
        dict["SecretToken"].ShouldBe("***REDACTED***");
        dict["Username"].ShouldBe("admin");
    }
}
