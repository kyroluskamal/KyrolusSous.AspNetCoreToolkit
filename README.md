# KyrolusSous.AspNetCoreToolkit

This toolkit bundles Serilog bootstrapping plus Entity Framework repositories (runtime + source generator) to cut boilerplate and stay AOT-friendly.

## Contents
- KyrolusSous.Logging (Serilog bootstrapper)
- KyrolusSous.Repositories.EF.Abstractions (contracts, helpers, policies, observer)
- KyrolusSous.Repositories.EF.Runtime (repository + unit of work implementations)
- KyrolusSous.Repositories.EF.Generator (source generator for EF repositories)
- Tests

---

## KyrolusSous.Logging
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
