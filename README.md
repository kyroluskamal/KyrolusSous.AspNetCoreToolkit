# KyrolusSous.AspNetCoreToolkit

This toolkit bundles Serilog bootstrapping plus Entity Framework repositories (runtime + source generator) to cut boilerplate and stay AOT-friendly.

## Contents
- KyrolusSous.Logging.Abstractions (logging contracts)
- KyrolusSous.Logging.Runtime (Microsoft.Extensions.Logging adapter)
- KyrolusSous.Logging.Serilog (Serilog bootstrapper)
- KyrolusSous.Mediator (abstractions + runtime + generator)
- KyrolusSous.Repositories.EF.Abstractions (contracts, helpers, policies, observer)
- KyrolusSous.Repositories.EF.Runtime (repository + unit of work implementations)
- KyrolusSous.Repositories.EF.Generator (source generator for EF repositories)
- Tests

---

## KyrolusSous.Logging.Serilog
Opinionated Serilog setup with sane defaults, flexible options, and a customizable console formatter.

**What you get**
- Extensions: `AddKyrolusLogging(IConfiguration, Action<LoggingOptions>?)` and `UseKyrolusLogging(IHostBuilder)`.
- Defaults: enrichers (Application, MachineName, ProcessId, ThreadId, EnvironmentName) + sinks (Console, File: `Logs/log-.txt`).
- Reflection wiring: supports common sinks/enrichers by enum, plus custom types/methods.
- Strictness: `ThrowIfPackageMissing` (default true) fails fast when a sink/enricher package is missing.
- Console formatter: per-sink options (`TextFormatterOptions`) for properties, source, exception detail per level, and colors.

**Install sinks/enrichers**
```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
# optional: Serilog.Sinks.Seq / MSSqlServer / Elasticsearch / PostgreSQL / SQLite, and enrichers (Thread, Process, Environment)
```

**Quick start**
```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKyrolusLogging(builder.Configuration, opts =>
{
    opts.ThrowIfPackageMissing = false; // relax in production
    opts.FormatterOptionsBySink["Console"] = new TextFormatterOptions { UseColors = true, ShowProperties = false };

    // Native AOT: avoid reflection/Assembly.Load
    // opts.UseReflectionDiscovery = false;
    // opts.AotSinkRegistrations.Add(cfg => cfg.WriteTo.Console());
    // opts.AotEnricherRegistrations.Add(enr => enr.WithProperty("Feature", "AOT"));
});

builder.Host.UseKyrolusLogging();
var app = builder.Build();
app.MapGet("/", (ILogger<Program> log) => { log.LogInformation("Hello"); return "OK"; });
app.Run();
```

**Config via appsettings (optional)**
`AddKyrolusLogging` binds from the `Logging` section:
```json
"Logging": {
  "MinimumLevel": "Information",
  "Sinks": [
    { "commonType": "Console" },
    { "commonType": "File", "sinkOptions": { "path": "Logs/log-.txt", "rollingInterval": "Day" } }
  ],
  "FormatterOptionsBySink": {
    "Console": { "useColors": true, "showProperties": false, "exceptionDetail": "MessageOnly" }
  }
}
```

**Tests**: `Tests/KyrolusSous.Logging.Tests` (unit + integration).

---

## KyrolusSous.ExceptionHandling (Abstractions + Runtime + Optional packages)
Consistent, AOT-friendly error handling with clean contracts, pluggable mappers, and optional integrations.

**What you get**
- **Abstractions**: `KyrolusErrorEnvelope`, `KyrolusErrorItem`, `KyrolusErrorContext`, `KyrolusExceptionMapping`, base `KyrolusException`.
- **Runtime**: middleware + MVC filter, default mappers, JSON writer, and a background-safe translator.
- **Optional**: FluentValidation mapping + ProblemDetails writer.
- **Security**: stack traces hidden in production by default.
- **Localization**: optional message localizer (`IKyrolusErrorLocalizer`).

### Default JSON response
```json
{
  "code": "not_found",
  "title": "Not found",
  "detail": "Order 123 was not found",
  "traceId": "00-acde..."
}
```

### Register (Minimal APIs)
```csharp
builder.Services.AddKyrolusExceptionHandling(options =>
{
    options.IncludeExceptionDetailsInResponse = false;
    options.IncludeExceptionDetailsInDevelopment = true;
    options.CorrelationIdHeaderName = "X-Correlation-ID";
});

var app = builder.Build();
app.UseKyrolusExceptionHandling();
```

### MVC filter
```csharp
builder.Services.AddControllers(o =>
{
    o.Filters.Add<KyrolusExceptionFilter>();
});
```

### ProblemDetails writer (optional)
```csharp
builder.Services.AddKyrolusProblemDetailsWriter();
```

### FluentValidation mapping (optional)
```csharp
builder.Services.AddKyrolusFluentValidationExceptionHandling();
```

### Entity Framework & Redis mappings (optional)
```csharp
builder.Services.AddKyrolusEntityFrameworkExceptionHandling();
builder.Services.AddKyrolusRedisExceptionHandling();
```

### Custom domain exception + mapper
```csharp
public sealed class OrderLockedException(Guid id)
    : KyrolusException(HttpStatusCode.Conflict, "order_locked", "Order locked", $"Order {id} is locked");

public sealed class OrderLockedMapper : IKyrolusExceptionMapper
{
    public int Order => -100; // higher priority
    public bool TryMap(Exception ex, KyrolusErrorContext ctx, out KyrolusExceptionMapping mapping)
    {
        if (ex is not OrderLockedException domain)
        {
            mapping = null!;
            return false;
        }

        mapping = new KyrolusExceptionMapping(
            new KyrolusErrorEnvelope(domain.Code, domain.Title, domain.Detail, ctx.TraceId),
            domain.StatusCode);
        return true;
    }
}

services.AddSingleton<IKyrolusExceptionMapper, OrderLockedMapper>();
```

### Background/worker translation (no HTTP)
```csharp
try
{
    // work
}
catch (Exception ex)
{
    var translator = provider.GetRequiredService<KyrolusExceptionTranslator>();
    var context = new KyrolusErrorContext(
        TraceId: Activity.Current?.Id,
        CorrelationId: null,
        UserId: null,
        TenantId: null,
        Path: null,
        Method: null,
        Culture: null);

    KyrolusErrorResult result = translator.Translate(ex, context);
    // return result or log it
}
```

### Localization
```csharp
public sealed class DictionaryErrorLocalizer : IKyrolusErrorLocalizer
{
    private readonly Dictionary<string, string> translations = new()
    {
        ["not_found"] = "Not found",
        ["not_found.detail"] = "The requested item was not found"
    };

    public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
        => translations.TryGetValue(code, out var value) ? value : defaultMessage;
}

services.AddSingleton<IKyrolusErrorLocalizer, DictionaryErrorLocalizer>();
```

---

## KyrolusSous.Mediator (Abstractions + Runtime + Generator)
Mediator pipeline with requests, commands, queries, notifications, streaming, processors, and exception handling.

**Key concepts**
- Requests: `IKyrolusRequest<TResponse>` (generic), plus `IKyrolusQuery<TResponse>` and `IKyrolusCommand`/`IKyrolusCommand<TResponse>`.
- Notifications: `INotification` + `INotificationHandler<T>`.
- Streaming: `IKyrolusStreamRequest<T>` + `IKyrolusStreamRequestHandler<TRequest,TResponse>`.
- Pipeline behaviors: `IKyrolusPipelineBehavior<TRequest,TResponse>` (ordered via `PipelineOrderAttribute`).
- Pre/Post processors: `IKyrolusRequestPreProcessor<TRequest>`, `IKyrolusRequestPostProcessor<TRequest,TResponse>`.
- Exception pipeline: `IKyrolusRequestExceptionAction<TRequest,TException>` and `IKyrolusRequestExceptionHandler<TRequest,TException,TResponse>`.
- Publish strategy: `IKyrolusNotificationPublishStrategy` (parallel/sequential) + per-call override.

### Runtime usage (reflection-based scanning)
Best for non-AOT apps. No generator required.
```csharp
using KyrolusSous.Mediator.Runtime.Config;
using System.Reflection;

builder.Services.AddKyrolusMediatorFromAssemblies(
    Assembly.GetExecutingAssembly(),
    typeof(SomeExternalHandler).Assembly);

var mediator = app.Services.GetRequiredService<IKyrolusMediator>();
```

### Manual DI (AOT-friendly without generator)
Register handlers explicitly and skip scanning.
```csharp
builder.Services.AddKyrolusMediator();
builder.Services.AddTransient<IKyrolusQueryHandler<GetUserQuery, UserDto>, GetUserHandler>();
builder.Services.AddTransient<IKyrolusCommandHandler<CreateUserCommand>, CreateUserHandler>();
builder.Services.AddTransient<INotificationHandler<UserCreated>, UserCreatedHandler>();
```

### AOT usage (generator)
Use the generator for static dispatch and AOT-friendly startup.
```csharp
builder.Services.AddKyrolusMediator();
builder.Services.AddKyrolusMediatorGeneratedDispatcher();
builder.Services.AddKyrolusMediatorHandlers();
builder.Services.AddKyrolusMediatorNotificationHandlers();
```

**Choosing a mode**
- Reflection scanning (`AddKyrolusMediatorFromAssemblies`) = easy, not AOT-friendly.
- Manual DI = AOT-friendly, but you register handlers yourself.
- Generator = AOT-friendly, auto-registration and static dispatcher.

### Example handlers
```csharp
public sealed record GetUserQuery(Guid Id) : IKyrolusQuery<UserDto>;
public sealed class GetUserHandler : IKyrolusQueryHandler<GetUserQuery, UserDto>
{
    public Task<UserDto> Handle(GetUserQuery request, CancellationToken ct) => /* ... */;
}

public sealed record CreateUserCommand(string Name) : IKyrolusCommand;
public sealed class CreateUserHandler : IKyrolusCommandHandler<CreateUserCommand>
{
    public Task Handle(CreateUserCommand request, CancellationToken ct) => /* ... */;
}

public sealed record UserCreated(Guid Id) : INotification;
public sealed class UserCreatedHandler : INotificationHandler<UserCreated>
{
    public Task Handle(UserCreated notification, CancellationToken ct) => /* ... */;
}

public sealed record ListUsersStream() : IKyrolusStreamRequest<UserDto>;
public sealed class ListUsersStreamHandler : IKyrolusStreamRequestHandler<ListUsersStream, UserDto>
{
    public async IAsyncEnumerable<UserDto> Handle(ListUsersStream request, [EnumeratorCancellation] CancellationToken ct)
    {
        yield break;
    }
}
```

### Sending, publishing, and streaming
```csharp
var mediator = provider.GetRequiredService<IKyrolusMediator>();

var user = await mediator.SendAsync(new GetUserQuery(id));
await mediator.SendAsync(new CreateUserCommand("Ali"));
await mediator.PublishAsync(new UserCreated(id));

await foreach (var item in mediator.StreamAsync(new ListUsersStream(), ct))
{
    // process item
}
```

### Pipeline behaviors (ordering)
```csharp
[PipelineOrder(-100)]
public sealed class AuditBehavior<TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TRequest : IKyrolusRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        // before
        var result = await next();
        // after
        return result;
    }
}
```

### Publish strategy (parallel vs sequential)
```csharp
builder.Services.UseKyrolusMediatorSequentialNotifications(); // or Parallel

var publisher = provider.GetRequiredService<IKyrolusMediatorPublisher>();
await publisher.PublishAsync(new UserCreated(id), new KyrolusParallelNotificationPublishStrategy(), ct);
```

### MediatR compatibility (optional)
Compatibility interfaces are available in:
`KyrolusSous.Mediator.Abstractions.Compatibility`.
```csharp
public sealed record Ping() : KyrolusSous.Mediator.Abstractions.Compatibility.IRequest<Pong>;
public sealed class PingHandler :
    KyrolusSous.Mediator.Abstractions.Compatibility.IRequestHandler<Ping, Pong>
{
    public Task<Pong> Handle(Ping request, CancellationToken ct) => Task.FromResult(new Pong());
}
```

---

## KyrolusSous.Repositories.EF.Runtime
Async generic repository + unit-of-work helpers that mirror most generator features while staying AOT-friendly.

**Key capabilities**
- Implements `IKyrolusRepositoryAsync<TContext, TEntity, TKey>` with compound key support (`object?[]`) and overloads for string includes or expression includes.
- Optional caching (enable via ctor `enableCaching` and `cacheTtlSeconds`) with per-id and “all” keys; invalidation on Add/Update/Patch/Remove/ExecuteUpdate/ExecuteDelete.
- Compiled queries (runtime) for the common paths: `GetByIdCompiledAsync` (single key, no global filter) and `GetAllCompiledAsync` (trivial filter, no global filter) using `EF.CompileAsyncQuery`.
- Observer hooks (`IKyrolusRepositoryObserver`) around every operation, plus `KyrolusRepositoryPolicy` defaults (as-no-tracking, split-query, soft-delete, global filter, concurrency retry).
- Soft delete via policy defaults (`EnableSoftDeleteDefault` and property name `IsDeleted`), concurrency retries via `ConcurrencyHelper`, bulk fallback via `IKyrolusBulkExecutor` when supplied.
- No reflection for core operations; uses EF metadata/entries to set values (AOT safer).

**DI registration**
- Runtime fallback (open generic):  
  ```csharp
  services.AddKyrolusRuntimeRepositories(); // registers IKyrolusRepositoryAsync<,,> -> KyrolusRepositoryAsync<,,>
  ```
- If you also use generated repos, register this first, then call the generated extension `AddGeneratedKyrolusRepositories()` so the generated types override the fallback.

**Constructor (runtime repo)**
```csharp
new KyrolusRepositoryAsync<AppDb, Order, int>(
    db,
    policy: new KyrolusRepositoryPolicy { AsNoTrackingDefault = true, UseSplitQueryDefault = false },
    observer: myObserver,
    bulkExecutor: null,
    cache: myCacheProvider,
    enableCaching: true,
    cacheTtlSeconds: 120);
```

**Common usage**
```csharp
var repo = provider.GetRequiredService<IKyrolusRepositoryAsync<AppDb, Order, int>>();
var order = await repo.GetByIdCompiledAsync(1);
var active = await repo.GetAllCompiledAsync(o => o.Status == Status.Active); // falls back when filter/global filter require

var page = await repo.GetPagedAsync(new OrderPagedSpec(1, 20), cancellationToken);
await repo.ExecuteUpdateAsync(o => o.Status == Status.Draft,
    setters => setters.SetProperty(o => o.Status, Status.Active),
    cancellationToken: cancellationToken);
```

---

## KyrolusSous.Repositories.Marten (Abstractions + Runtime)
Generic Marten repositories مع خيارات متقدمة (Specifications، Soft Delete، Event Store، Saga، Projections، Decorators، UoW).

**Register**
```csharp
// builder.Services.AddMarten(...); // your Marten setup
builder.Services.AddKyrolusMartenRuntime(opts =>
{
    opts.AutoStart = true;
    opts.ShardsToStart = new[] { "ProjectionName" };     // optional
    opts.RebuildProjections = new[] { "ProjectionName" }; // optional
    opts.WaitForNonStaleTimeout = TimeSpan.FromSeconds(30);
    // opts.ConfigureSettings = settings => { /* daemon settings via reflection */ };
});
```
Default registrations are provided via `TryAdd*` (no-op or permissive) for:
`IKyrolusMartenObserver`, `IKyrolusMartenAuthorization`, `IKyrolusMartenValidation`,
`IKyrolusMartenSoftDeletePolicy`, `IKyrolusMartenCacheProvider`,
`IKyrolusMartenResiliencePolicy`, `IKyrolusMartenTracing`.
Register your own implementations to override them.

**Basic repository usage**
```csharp
// inject IKyrolusMartenRepositoryAsync<IDocumentSession, Order, Guid>
var repo = provider.GetRequiredService<IKyrolusMartenRepositoryAsync<IDocumentSession, Order, Guid>>();
var result = await repo.GetByIdAsync(id);
var order = result?.Entity;
var version = result?.Version;

var page = await repo.QueryPageAsync(
    new MartenQueryOptions<Order>(Filter: o => o.Status == Status.Active),
    q => q,
    new MartenPageRequest(PageNumber: 1, PageSize: 20));

var patched = await repo.PatchAsync(id, new() { ["Status"] = Status.Archived });
var patchedVersion = patched?.Version;

await foreach (var item in repo.StreamAsync(
    new MartenQueryOptions<Order>(Filter: o => o.Status == Status.Active),
    cancellationToken: ct)) { /* ... */ }
```

**Soft delete**
```csharp
// inject IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, Product, Guid>
await softRepo.RemoveAsync(product);          // sets IsDeleted (or configured property)
await softRepo.RestoreAsync(product.Id);      // clears IsDeleted
var active = await softRepo.GetAllAsync(new MartenQueryOptions<Product>(IncludeSoftDeleted: false));
```

**Unit of Work (per session)**
```csharp
var uow = provider.GetRequiredService<IKyrolusMartenUnitOfWork<IDocumentSession>>();
var orders = await uow.Get<Order, Guid>().GetAllAsync();
await uow.SaveChangesAsync(ct); // single session save
```

**Specifications**
```csharp
public sealed class ActiveOrdersSpec : IQuerySpecification<Order>
{
    public IMartenQueryable<Order> Apply(IMartenQueryable<Order> query)
        => query.Where(o => o.Status == Status.Active);
}
var active = await repo.QueryAsync(new MartenQueryOptions<Order>(Specification: new ActiveOrdersSpec()), q => q);
```

**Patching**
```csharp
var patched = await repo.PatchAsync(id, new() { ["Status"] = Status.Active });
await repo.PatchWhereAsync(o => o.Status == Status.Draft, new() { ["Status"] = Status.Active });
```

**Streaming**
```csharp
await foreach (var order in repo.StreamAsync(
    new MartenQueryOptions<Order>(Filter: o => o.Status == Status.Active),
    cancellationToken: ct)) { /* process */ }
```

**Event Store**
```csharp
var eventStore = provider.GetRequiredService<IKyrolusMartenEventStore>();
await eventStore.AppendAsync(streamId, new object[] { new OrderCreated(...), new OrderApproved(...) });
var stream = await eventStore.LoadStreamAsync<OrderAggregate>(streamId);
```

**Saga**
```csharp
var saga = provider.GetRequiredService<IKyrolusMartenSagaCoordinator>();
await saga.StartAsync("saga-id", new PaymentSagaState { Step = 1 });
await saga.UpdateAsync("saga-id", state => state with { Step = 2 });
```

**Projections daemon**
```csharp
var orchestrator = provider.GetRequiredService<IKyrolusMartenProjectionOrchestrator>();
await orchestrator.EnqueueRebuildAsync("ProjectionName");
await orchestrator.EnsureUpToDateAsync("ProjectionName");
```

**Decorator (caching/resilience/tracing/authorization/validation)**
```csharp
var decorated = services.CreateDecoratedRepository<IDocumentSession, Order, Guid>(
    session,
    new KyrolusMartenRepositoryDependencies
    {
        CacheProvider = cache,
        ResiliencePolicy = resilience,
        Tracing = tracing,
        Authorization = authorization,
        Validation = validation
    });
```

### Marten Authorization Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Authorization`.
Plug them into `KyrolusMartenRepositoryDependencies.Authorization` or use directly.

**Allow all**
Use for dev or when you want to bypass checks.
```csharp
var auth = KyrolusMartenAllowAllAuthorization.Instance;
```

**Deny all**
Use for lock-down or maintenance mode.
```csharp
var auth = KyrolusMartenDenyAllAuthorization.Instance;
```

**Delegate**
Custom lambda-based rule.
```csharp
var auth = new KyrolusMartenDelegateAuthorization((op, target, ct) =>
{
    if (op == "RemoveById") return Task.FromResult(false);
    return Task.FromResult(true);
});
```

**Operation whitelist**
Allow only specific operations.
```csharp
var auth = new KyrolusMartenOperationWhitelistAuthorization(new[] { "GetAll", "GetById" });
```

**Operation blacklist**
Block specific operations.
```csharp
var auth = new KyrolusMartenOperationBlacklistAuthorization(new[] { "RemoveById", "DeleteWhere" });
```

**Operation prefix**
Allow only operations with a prefix.
```csharp
var auth = new KyrolusMartenOperationPrefixAuthorization(new[] { "Get", "Query" });
```

**Operation map**
Different rule per operation, with fallback.
```csharp
var auth = new KyrolusMartenOperationMapAuthorization(new Dictionary<string, IKyrolusMartenAuthorization>
{
    ["GetAll"] = KyrolusMartenAllowAllAuthorization.Instance,
    ["RemoveById"] = KyrolusMartenDenyAllAuthorization.Instance
});
```

**Composite (all)**
All rules must pass.
```csharp
var auth = new KyrolusMartenCompositeAllAuthorization(new IKyrolusMartenAuthorization[]
{
    new KyrolusMartenOperationPrefixAuthorization(new[] { "Get" }),
    new KyrolusMartenOperationBlacklistAuthorization(new[] { "GetById" })
});
```

**Composite (any)**
Any rule passing is enough.
```csharp
var auth = new KyrolusMartenCompositeAnyAuthorization(new IKyrolusMartenAuthorization[]
{
    new KyrolusMartenOperationWhitelistAuthorization(new[] { "GetAll" }),
    new KyrolusMartenOperationWhitelistAuthorization(new[] { "GetPage" })
});
```

**Target type map**
Pick authorization based on payload type.
```csharp
var auth = new KyrolusMartenTargetTypeAuthorization(new Dictionary<Type, IKyrolusMartenAuthorization>
{
    [typeof(Order)] = new KyrolusMartenOperationWhitelistAuthorization(new[] { "Add", "Update" }),
    [typeof(Payment)] = new KyrolusMartenOperationBlacklistAuthorization(new[] { "RemoveById" })
});
```

**Tenant match**
Ensure the target tenant matches the current tenant.
```csharp
var auth = new KyrolusMartenTenantMatchAuthorization(
    tenantResolver,
    target => (target as ITenantScoped)?.TenantId,
    allowWhenUnknown: false);
```

**Role based**
Authorize using roles from a context object.
```csharp
var auth = new KyrolusMartenRoleAuthorization(new[] { "Admin" });
var ctx = new KyrolusMartenAuthorizationContext(UserId: "u1", Roles: new[] { "Admin" });
var allowed = await auth.AuthorizeAsync("Add", ctx);
```

**Permission based**
Authorize using permissions from a context object.
```csharp
var auth = new KyrolusMartenPermissionAuthorization(new[] { "order.write" });
var ctx = new KyrolusMartenAuthorizationContext(UserId: "u1", Permissions: new[] { "order.write" });
var allowed = await auth.AuthorizeAsync("Update", ctx);
```

### Marten Validation Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Validation`.
Plug them into `KyrolusMartenRepositoryDependencies.Validation`.

**No-op**
Disable validation.
```csharp
var validation = KyrolusMartenNoopValidation.Instance;
```

**Delegate**
Custom validation logic.
```csharp
var validation = new KyrolusMartenDelegateValidation((op, payload, ct) =>
{
    if (op == "Add" && payload is Order o && o.Total <= 0)
        throw new KyrolusMartenValidationException("Total must be > 0");
    return Task.CompletedTask;
});
```

**Payload not null**
```csharp
var validation = new KyrolusMartenPayloadNotNullValidation();
```

**Payload type**
```csharp
var validation = new KyrolusMartenPayloadTypeValidation(new[] { typeof(Order) }, allowNull: false);
```

**String length**
```csharp
var validation = new KyrolusMartenStringLengthValidation(minLength: 3, maxLength: 64);
```

**Collection count**
```csharp
var validation = new KyrolusMartenCollectionCountValidation(minCount: 1, maxCount: 100);
```

**Validatable payload (sync/async)**
```csharp
public sealed class Order : IKyrolusMartenValidatable
{
    public decimal Total { get; set; }
    public void Validate()
    {
        if (Total <= 0) throw new KyrolusMartenValidationException("Total must be > 0");
    }
}
var validation = new KyrolusMartenValidatablePayloadValidation();
```

**Operation map**
```csharp
var validation = new KyrolusMartenOperationMapValidation(new Dictionary<string, IKyrolusMartenValidation>
{
    ["Add"] = new KyrolusMartenPayloadNotNullValidation(),
    ["Update"] = new KyrolusMartenValidatablePayloadValidation()
});
```

**Composite**
```csharp
var validation = new KyrolusMartenCompositeValidation(new IKyrolusMartenValidation[]
{
    new KyrolusMartenPayloadNotNullValidation(),
    new KyrolusMartenValidatablePayloadValidation()
}, stopOnFirst: false);
```

**Exceptions**
- `KyrolusMartenValidationException` for a single failure.
- `KyrolusMartenAggregateValidationException` for multiple errors in composite mode.

### Marten Observer Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Observer`.
Plug them into `KyrolusMartenRepositoryDependencies.Observer` or use directly.

**No-op**
Disable observer hooks.
```csharp
var observer = KyrolusMartenNoopObserver.Instance;
```

**Delegate**
Custom hooks per operation.
```csharp
var observer = new KyrolusMartenDelegateObserver(
    onBefore: (op, payload, ct) =>
    {
        Console.WriteLine($"Starting {op}");
        return Task.CompletedTask;
    },
    onAfter: (op, result, elapsed, ex, ct) =>
    {
        Console.WriteLine($"Finished {op} in {elapsed.TotalMilliseconds} ms");
        return Task.CompletedTask;
    });
```

**Debug output**
Quick tracing via `Debug.WriteLine`.
```csharp
var observer = new KyrolusMartenDebugObserver();
```

**Errors only**
Notify only when an operation fails.
```csharp
var observer = new KyrolusMartenErrorOnlyObserver((op, result, elapsed, ex, ct) =>
{
    Console.WriteLine($"Error in {op}: {ex.Message}");
    return Task.CompletedTask;
});
```

**Slow operations**
Trigger when duration exceeds a threshold.
```csharp
var observer = new KyrolusMartenSlowOperationObserver(
    TimeSpan.FromMilliseconds(250),
    (op, result, elapsed, ct) =>
    {
        Console.WriteLine($"Slow {op}: {elapsed.TotalMilliseconds} ms");
        return Task.CompletedTask;
    });
```

**Operation filter**
Observe only certain operations.
```csharp
var observer = new KyrolusMartenOperationFilterObserver(
    op => op.StartsWith("Get", StringComparison.Ordinal),
    new KyrolusMartenDebugObserver());
```

**Composite**
Chain multiple observers together.
```csharp
var observer = new KyrolusMartenCompositeObserver(new IKyrolusMartenObserver[]
{
    new KyrolusMartenDebugObserver(),
    new KyrolusMartenCountingObserver()
});
```

**Counting**
Collect per-operation counts.
```csharp
var observer = new KyrolusMartenCountingObserver();
// later:
var snapshot = observer.Snapshot();
```

### Marten Cache Provider Use Cases
Implementations are intentionally external (so you can plug IMemoryCache, IDistributedCache, Redis, etc.).
Hook it via `KyrolusMartenRepositoryDependencies.CacheProvider`.

**No-op**
Use when you want to disable caching.
```csharp
public sealed class KyrolusMartenNoopCacheProvider : IKyrolusMartenCacheProvider
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);
    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
```

**In-memory cache**
Fast for single-instance apps or tests.
```csharp
public sealed class KyrolusMartenMemoryCacheProvider(IMemoryCache cache) : IKyrolusMartenCacheProvider
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(cache.TryGetValue(key, out var value) ? (T?)value : default);

    public Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        cache.Set(key, value, ttl);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        cache.Remove(key);
        return Task.CompletedTask;
    }
}
```

**Distributed cache**
Good for multi-instance deployments.
```csharp
public sealed class KyrolusMartenDistributedCacheProvider(IDistributedCache cache, JsonSerializerOptions? options = null)
    : IKyrolusMartenCacheProvider
{
    private readonly JsonSerializerOptions json = options ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var bytes = await cache.GetAsync(key, cancellationToken).ConfigureAwait(false);
        return bytes is null ? default : JsonSerializer.Deserialize<T>(bytes, json);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, json);
        await cache.SetAsync(key, bytes, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        => cache.RemoveAsync(key, cancellationToken);
}
```

**Tenant-aware keys**
Use when multi-tenancy is enabled to avoid cross-tenant leakage.
```csharp
var key = $"{tenantId}:Order:{id}";
```

**TTL strategy**
Use short TTL for list queries, longer TTL for GetById.
Example: 10-30s for list pages, 2-5 minutes for single entity.

### Marten Resilience Policy Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Resilience`.
Plug them into `KyrolusMartenRepositoryDependencies.ResiliencePolicy`.

**No-op**
Disable resilience handling.
```csharp
var policy = KyrolusMartenNoopResiliencePolicy.Instance;
```

**Delegate**
Wrap any custom logic.
```csharp
var policy = new KyrolusMartenDelegateResiliencePolicy(
    execute: async (op, action, ct) =>
    {
        Console.WriteLine($"Starting {op}");
        await action().ConfigureAwait(false);
    });
```

**Retry with fixed delay**
```csharp
var policy = new KyrolusMartenRetryResiliencePolicy(
    maxRetries: 3,
    delay: TimeSpan.FromMilliseconds(200),
    shouldRetry: ex => ex is TimeoutException);
```

**Retry with exponential backoff**
```csharp
var policy = new KyrolusMartenRetryResiliencePolicy(
    maxRetries: 5,
    delayFactory: attempt => TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt)));
```

**Timeout**
Fail if the operation takes too long.
```csharp
var policy = new KyrolusMartenTimeoutResiliencePolicy(TimeSpan.FromSeconds(2));
```

**Circuit breaker**
Open after N failures, then block for a window.
```csharp
var policy = new KyrolusMartenCircuitBreakerResiliencePolicy(
    failureThreshold: 5,
    breakDuration: TimeSpan.FromSeconds(30));
```

**Composite**
Combine multiple policies in order.
```csharp
var policy = new KyrolusMartenCompositeResiliencePolicy(new IKyrolusMartenResiliencePolicy[]
{
    new KyrolusMartenRetryResiliencePolicy(3, TimeSpan.FromMilliseconds(150)),
    new KyrolusMartenTimeoutResiliencePolicy(TimeSpan.FromSeconds(2))
});
```

### Marten Tracing Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Tracing`.
Plug them into `KyrolusMartenRepositoryDependencies.Tracing`.

**No-op**
Disable tracing.
```csharp
var tracing = KyrolusMartenNoopTracing.Instance;
```

**Delegate**
Custom hooks for scope/record.
```csharp
var tracing = new KyrolusMartenDelegateTracing(
    start: (op, payload) =>
    {
        Console.WriteLine($"Start {op}");
        return null;
    },
    record: (op, payload, elapsed, ex, ct) =>
    {
        Console.WriteLine($"End {op} in {elapsed.TotalMilliseconds} ms");
        return Task.CompletedTask;
    });
```

**ActivitySource**
Integrate with OpenTelemetry or built-in Activity tracing.
```csharp
var tracing = new KyrolusMartenActivityTracing("MyApp.Marten");
```

**Debug output**
Simple diagnostics.
```csharp
var tracing = new KyrolusMartenDebugTracing();
```

**In-memory**
Useful for tests or diagnostics in dev.
```csharp
var tracing = new KyrolusMartenInMemoryTracing();
var records = tracing.Snapshot();
```

**Operation filter**
Trace only specific operations.
```csharp
var tracing = new KyrolusMartenOperationFilterTracing(
    op => op.StartsWith("Get", StringComparison.Ordinal),
    new KyrolusMartenDebugTracing());
```

**Error-only**
Only record failures.
```csharp
var tracing = new KyrolusMartenErrorOnlyTracing(new KyrolusMartenDebugTracing());
```

**Threshold**
Record only slow operations.
```csharp
var tracing = new KyrolusMartenThresholdTracing(TimeSpan.FromMilliseconds(250), new KyrolusMartenDebugTracing());
```

**Sampling**
Reduce tracing volume.
```csharp
var tracing = new KyrolusMartenSamplingTracing(0.1, new KyrolusMartenActivityTracing("MyApp.Marten"));
```

**Composite**
Chain multiple tracers.
```csharp
var tracing = new KyrolusMartenCompositeTracing(new IKyrolusMartenTracing[]
{
    new KyrolusMartenActivityTracing("MyApp.Marten"),
    new KyrolusMartenInMemoryTracing()
});
```

**Soft delete policy & dependencies**
Pass `KyrolusMartenRepositoryDependencies` when creating the repo (or override via decorator) to supply:
- `IKyrolusMartenSoftDeletePolicy` (property name, enabled flag)
- `IKyrolusMartenObserver`, `IKyrolusMartenAuthorization`, `IKyrolusMartenValidation`
- `IKyrolusMartenCacheProvider`, `IKyrolusMartenResiliencePolicy`, `IKyrolusMartenTracing`

### Marten Soft Delete Policy Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/SoftDelete`.
Plug them into `KyrolusMartenRepositoryDependencies.SoftDeletePolicy`.

**Disable soft delete**
```csharp
var policy = KyrolusMartenNoSoftDeletePolicy.Instance;
```

**Custom property**
```csharp
var policy = KyrolusMartenSoftDeletePolicy.For("IsArchived", filterDeletedByDefault: true);
```

**Default property (`IsDeleted`)**
```csharp
var policy = KyrolusMartenSoftDeletePolicy.IsDeleted();
```

### Marten Specification Use Cases
Implementations live in `Src/KyrolusSous.Repositories.Marten.Abstractions/Specifications`.

**Delegate**
```csharp
var spec = new KyrolusMartenDelegateSpecification<Order>(q => q.Where(o => o.Status == Status.Active));
```

**Filter**
```csharp
var spec = new KyrolusMartenFilterSpecification<Order>(o => o.Status == Status.Active);
```

**Order**
```csharp
var spec = new KyrolusMartenOrderSpecification<Order>(q => q.OrderBy(o => o.CreatedAt));
```

**Pagination**
```csharp
var spec = new KyrolusMartenPaginationSpecification<Order>(skip: 20, take: 10);
```

**Include**
Use Marten's include/graph API inside a specification.
```csharp
var spec = new KyrolusMartenIncludeSpecification<Order>(q =>
{
    q.Include<Order, Customer>(o => o.CustomerId, c => { });
});
```

**Composite**
```csharp
var spec = new KyrolusMartenCompositeSpecification<Order>(new IQuerySpecification<Order>[]
{
    new KyrolusMartenFilterSpecification<Order>(o => o.Status == Status.Active),
    new KyrolusMartenOrderSpecification<Order>(q => q.OrderByDescending(o => o.CreatedAt))
});
```

**Service registration summary**
- `IKyrolusMartenRepositoryAsync<,,> -> KyrolusMartenRepositoryAsync<,,>`
- `IKyrolusMartenSoftDeleteRepositoryAsync<,,> -> KyrolusMartenSoftDeleteRepositoryAsync<,,>`
- `IKyrolusMartenUnitOfWork<> -> KyrolusMartenUnitOfWork<>`
- `IKyrolusMartenEventStore -> KyrolusMartenEventStore`
- `IKyrolusMartenSagaCoordinator -> KyrolusMartenSagaCoordinator`
- `IKyrolusMartenProjectionOrchestrator -> KyrolusMartenProjectionOrchestrator`

---

## KyrolusSous.Repositories.EF.Generator (Source Generator)
Generates high-performance EF repositories to avoid runtime reflection and boilerplate, with caching, observer hooks, global filters, and compiled queries.

**Key outputs**
- Repository implementing `IKyrolusRepositoryAsync<TContext, TEntity, TKey>` plus soft-delete/bulk interfaces when enabled.
- Compiled queries (`EF.CompileAsyncQuery`) for `GetByIdCompiledAsync`, `GetAllCompiledAsync()`, and `GetAllCompiledAsync(filter)` (filtered). When a global filter is configured, compiled paths automatically fall back to the regular query to keep filters correct.
- Optional caching via your `ICacheProvider` (constructor optional): TTL (`CacheTtlSeconds`), per-id keys, an “all” key, and invalidation on Add/AddRange/Update/UpdateRange/Patch/Remove/RemoveRange/Restore/TryRestore/ExecuteUpdate/ExecuteDelete and bulk insert/upsert paths.
- Observer hooks (`IKyrolusRepositoryObserver`) around every operation, carrying diagnostics such as result counts and paging totals; concurrency retries bubble attempt counts via `ConcurrencyHelper`.
- Global query filter delegate (`KyrolusRepositoryPolicy.GlobalQueryFilter`) for multi-tenant or dynamic filtering; applied to all queries.
- Policy defaults (`KyrolusRepositoryPolicy`): `AsNoTrackingDefault`, `UseSplitQueryDefault`, `EnableSoftDeleteDefault`, `ConcurrencyRetryCount`, `ConcurrencyRetryDelay`, `DefaultPageSize`.
- Optional soft delete (property name configurable), row version support, split-query default, and default includes baked into generated queries.
- Attribute source is injected (no extra runtime assembly needed).

**Annotate your entity**
```csharp
[KyrolusEfRepository(
    typeof(AppDbContext),
    typeof(Order),
    typeof(OrderKey),
    "Id", "TenantId",
    RepositoryName = "OrderRepository",
    Namespace = "MyApp.Data",
    IncludeProperties = new[] { "Items", "Customer" },
    AsNoTracking = true,
    EnableSoftDelete = true,
    SoftDeleteProperty = "IsDeleted",
    UseSplitQuery = false,
    EnableBulk = true,
    EnableCaching = true,
    CacheTtlSeconds = 120,
    RowVersionProperty = "RowVersion")]
internal partial class OrderRepoMarker {}
```

**Use the generated repo**
```csharp
// After build, DI register and consume the generated type:
services.AddScoped<OrderRepository>();
```

**AOT note**: generated code avoids reflection; for caching, provide an AOT-safe `ICacheProvider` (e.g., Redis implementation). Global filters run via the supplied delegate and are applied to all queries; compiled queries bypassed when a global filter is present to keep filters correct.

**Advanced options recap**
- Soft delete: set `EnableSoftDelete` + `SoftDeleteProperty` to mark deletes instead of removing rows.
- Row version: set `RowVersionProperty` for optimistic concurrency wiring.
- Defaults: `AsNoTracking`, `UseSplitQuery`, `IncludeProperties` (default includes on every query).
- Caching: `EnableCaching`, `CacheTtlSeconds`; invalidated on mutations and server-side executes/bulk.
- Global filters: set `KyrolusRepositoryPolicy.GlobalQueryFilter` to enforce multi-tenant or dynamic filters everywhere.
- Observability: implement `IKyrolusRepositoryObserver` to tap before/after operations with payloads (counts, paging totals, retries).
- Bulk: plug `IKyrolusBulkExecutor` for server-side bulk ops; generator falls back to EF + SaveChanges if none is provided.

---

## Query Helpers (filters/order/includes)
Reusable contracts live in `Src/KyrolusSous.Repositories.EF.Abstractions/Query/QueryPrimitives.cs`:
- `IQueryHelper<TEntity>`
- `QueryRequest`, `OrderClause`, `FilterClause`, `QueryParts<TEntity>`

The generator emits a typed helper per entity (e.g., `TenantQueryHelper : IQueryHelper<Tenant>`) and registers it in DI via `AddGeneratedKyrolusRepositories()`. Use the abstraction directly in any project (including EasyAPI) without referencing the generator assembly.

**Supported operators**
- `string`: `contains`, `startswith`, `endswith`, `eq`
- `numeric/date`: `eq`, `gt`, `gte`, `lt`, `lte`
- `bool/guid/enum`: `eq`
- `includes`: whitelist of navigation properties generated per entity
- `orderBy`: any property name (whitelist per entity)

**Typical endpoint usage**
```csharp
using KyrolusSous.Repositories.EF.Abstractions.Query;
using KyrolusSous.Repositories.EF.Generated;

group.MapGet("/", async (
    KyrolusUnitOfWork uow,
    IQueryHelper<Tenant> helper,
    [FromQuery] string[]? includes,
    [FromQuery] string[]? orderBy,
    [FromQuery] string? name,
    [FromQuery] int? minUsers,
    CancellationToken ct) =>
{
    var request = new QueryRequest(
        Includes: includes,
        OrderBy: orderBy?.Select(o => o.EndsWith(":desc", StringComparison.OrdinalIgnoreCase)
            ? new OrderClause(o.Replace(":desc", "", StringComparison.OrdinalIgnoreCase), true)
            : new OrderClause(o)).ToArray(),
        Filters: new[]
        {
            name is null ? null : new FilterClause("Name", "contains", name),
            minUsers is null ? null : new FilterClause("UsersCount", "gte", minUsers.Value.ToString())
        }.Where(f => f is not null).ToArray()!
    );

    var parts = helper.Build(request);
    var items = await uow.Tenant.GetAllAsync(parts.Filter, parts.OrderBy, includeExpressions: parts.Includes, cancellationToken: ct);
    return Results.Ok(items);
});
```

**HTTP examples**
- Filter + order + includes:  
  `GET /api/tenant?name=soft&minUsers=10&orderBy=Name:desc&includes=Stores&includes=Users`
- Only includes:  
  `GET /api/tenant?includes=Stores`
- Only order:  
  `GET /api/tenant?orderBy=CreatedAt:desc`
- String ops (contains/startswith/endswith/eq):  
  `GET /api/tenant?filters[0].property=Name&filters[0].operator=contains&filters[0].value=soft`
  (or map them in your endpoint params to build `FilterClause`)
- Numeric/date ops (gt/gte/lt/lte/eq):  
  `GET /api/product?filters[0].property=Price&filters[0].operator=gt&filters[0].value=100`
- Bool/guid/enum eq:  
  `GET /api/store?filters[0].property=IsActive&filters[0].operator=eq&filters[0].value=true`
- Multiple filters: build `filters[]` array in query string then convert to `FilterClause[]` inside the endpoint.

---

## Tests
- Runtime EF: `Tests/KyrolusSous.BaseRepositoryEF.Tests/KyrolusSous.BaseRepositoryEF.UnitTests` and `...IntegrationTests`.
- Generator: `GeneratorTests` ensures source generation succeeds.
- Logging: `Tests/KyrolusSous.Logging.Tests`.

Run all:
```
dotnet test
```
