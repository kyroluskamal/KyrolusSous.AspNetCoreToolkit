# Review Order

The order to review the libraries in, and where each one stands. Tick items off as you go.

## Why this order

A library is reviewed **after** everything it depends on. Finding a design problem in an
abstraction after reviewing the code built on top of it means redoing that work.

Within a phase the order is loose - what matters is not starting a phase before the one above it.

## Status key

| Mark | Meaning |
|---|---|
| ✅ | Reviewed and tested |
| 🔧 | In progress / Ready for review & tests |
| ⬜ | Not started |
| ⚡ | Exercised by the Marten FullPipeline suite - adding tests here is cheap, the harness exists |
| 🚫 | Needs no tests (contracts only, no logic) |

---

## Phase 1 - Foundations

| # | Project | Status | Notes |
|---|---|---|---|
| 1 | `Mediator.Abstractions` | ✅ | 51 types documented. Behaviour covered via `Mediator.Tests` |
| 2 | `Mediator.Runtime` | ✅ | 101 unit tests. 3 defects fixed, pipeline cached (100% coverage) |
| 3 | `Mediator.Reflection` | ✅ | 52 unit tests covering dispatch, exception unwrapping, Assembly scanning, open generics, and caching (100% coverage) |
| 4 | `Mediator.Generator` | ✅ | 14 tests incl. incremental caching. Roslyn component properties verified |
| 5 | `Validation.Abstractions` | ✅ | 100% coverage via unit tests in Runtime suite |
| 6 | `Validation.Runtime` | ✅ | 100 unit tests covering caching, negative TTL, composite validation, mappers, localizers, metrics, tracing, cancellation, and profiles (100% coverage) |
| 7 | `Validation.DataAnnotations` | ✅ | 8 unit tests covering null requests, property & object-level validation, DI registration, CancellationToken, and context propagation (100% coverage) |
| 8 | `Validation.DataAnnotations.Generator` | ✅ | Roslyn Incremental Generator for DataAnnotations validation code emission |
| 9 | `Validation.Fluent` | ✅ | Built-in lightweight fluent validation rules, validators, and extensions |
| 10 | `Validation.FluentValidation` | ✅ | 9 unit tests covering FluentValidation adapter, custom groups, severity mapping, Egyptian National ID, URL validation, and DI assembly scanning (100% coverage) |
| 11 | `Validation.FluentValidation.Scanning` | ✅ | 2 unit tests covering reflection-based assembly scanning for DI (100% coverage) |
| 12 | `Validation.Generator` | ✅ | 4 unit tests covering Roslyn Incremental Generator code emission for DI validators & profiles (100% coverage) |
| 13 | `ExceptionHandling.Abstractions` | ✅ | 100% coverage across built-in domain exceptions, models, registry, and metadata extractors |
| 14 | `ExceptionHandling.Runtime` | ✅ | 187 unit tests covering middleware, filter, options, translators, mappers, localizers, and sanitizers (100% coverage) |
| 15 | `ExceptionHandling.ProblemDetails` | ✅ | 9 unit tests covering RFC 7807 problem details writer, DI, and NativeAOT source generator context (100% coverage) |
| 16 | `ExceptionHandling.EntityFramework` | ✅ | 6 unit tests covering EF Core exception mapper and DI registration (100% coverage) |
| 17 | `ExceptionHandling.FluentValidation` | ✅ | 4 unit tests covering FluentValidation mapper, metadata extraction, and DI registration (100% coverage) |
| 18 | `ExceptionHandling.Redis` | ✅ | 6 unit tests covering Redis exception mapper and DI registration (100% coverage) |
| 19 | `Compression.Abstractions` | ✅ | 100% coverage across ICompressor, ICompressionProvider, CompressionAlgorithm contracts |
| 20 | `Compression.Core` | ✅ | 100% coverage across ResponseCompressionMiddleware, Options, CompressionExtensions, and KyrolusCompressionProvider |
| 21 | `Compression.Brotli` | ✅ | 100% coverage across BrotliCompressor and DI extensions (Pure .NET) |
| 22 | `Compression.Gzip` | ✅ | 100% coverage across GzipCompressor and DI extensions (Pure .NET) |
| 23 | `Compression.Deflate` | ✅ | 100% coverage across DeflateCompressor and DI extensions (Pure .NET) |
| 24 | `Compression.Zstd` | ✅ | 100% coverage across ZstdCompressor and DI extensions (ZstdSharp.Port) |
| 25 | `Compression.Lz4` | ✅ | 100% coverage across Lz4Compressor and DI extensions (K4os.Compression.LZ4) |
| 26 | `Compression.Snappy` | ✅ | 100% coverage across SnappyCompressor and DI extensions (Snappier) |
| 27 | `Caching.Abstractions` | ✅ | 94.4% line coverage. Serializers, multi-algorithm payload transformers, key factories, telemetry & policies |
| 28 | `Caching.Redis` | ✅ | Redis L2 & NearCache (L1+L2), Lua-based distributed locking, Pub/Sub bus, IDistributedCache & IOutputCacheStore adapters |
| 29 | `Caching.MessagePack` | ✅ | 100% line coverage. Binary MessagePack cache serializer with optional LZ4 block compression |
| 30 | `Mapping.Abstractions` | ✅ | 88.5% line coverage. Contracts, attributes, context, circular reference tracking, custom converters and resolvers |
| 31 | `Mapping.Runtime` | ✅ | 85.7% line coverage. Zero-dependency pure .NET mapper engine, nested mapping, collections, flattening, and LINQ projections |
| 32 | `Mapping.Generator` | ✅ | 88.6% line coverage. Roslyn Incremental Generator emitting pure C# 100% Native AOT mapping extension methods |
| 33 | `Logging.Abstractions` | ✅ | Contracts, Timers, LevelSwitch, Extensions (97.3% coverage) |
| 34 | `Logging.Core` | ✅ | Pure Core logging engine, DataMasker, StringRedactor, ExceptionSanitizer, LogRateLimiter, HttpMiddleware (94.6% coverage) |
| 35 | `Logging.Serilog` | ✅ | Serilog integration, ECS v1.12+ formatter, 8 Modern ANSI themes, Destructuring Policy, RateLimiting Filter, 64 tests (91.1% coverage) |

## Phase 2 - Repositories

| # | Project | Status | Notes |
|---|---|---|---|
| 35 | `Repositories.EF.Abstractions` | ✅ | Gate 65%/55%. Unparseable route keys throw FormatException (yielding 400 instead of 500), dynamic query & batch extensions |
| 36 | `Repositories.EF.Runtime` | ✅ | 475 integration + 106 unit tests. 15-round logical audit completed. Temporal tables, query tags, resilient retry, interceptors |
| 37 | `Repositories.EF.Generator` | ✅ | 133 integration + 67 unit tests. Roslyn Incremental Generator for EF repositories |
| 38 | `Repositories.EF.Cache.Distributed` | ✅ | Hybrid 2nd-level cache provider and distributed invalidation |
| 39 | `Repositories.Marten.Abstractions` | ✅ | Complete contracts: Outbox, Metadata, Upcasting, Keyset pagination, Multi-level IncludeGraph |
| 40 | `Repositories.Marten.Runtime` | ✅ | 235 integration + 28 unit tests. 15-round logical audit completed. JSON deep patching, upcasting pipeline, metadata, bulk COPY |
| 41 | `Repositories.Marten.Generator` | ✅ | Roslyn Incremental Generator for Marten repositories |

## Phase 3 - CQRS

| # | Project | Status | Notes |
|---|---|---|---|
| 42 | `CQRS.Abstractions` | ✅ | Authorization, Audit Trail, Transactional Outbox, Batching, Projections, LivePush, Idempotency, Transactional, Throttling, DomainEvents, OpenTelemetry Telemetry & Performance behaviors |
| 43 | `CQRS.Validation` | ✅ | Pipeline behavior with validation engine & multi-validator collection |
| 44 | `CQRS.Mapping` | ✅ | Entity and DTO mapping extensions |
| 45 | `CQRS.ExceptionHandling` | ✅ | Exception mapper behavior with structured translation |
| 46 | `CQRS.Caching` | ✅ | Query caching, Command invalidation, and Idempotent command deduplication behavior |
| 47 | `CQRS.EF` | ✅ | Generic CRUD Commands, Queries, Keyset/Seek pagination, Specification Queries, Atomic DbContext transaction behavior, and DomainEvents dispatching |
| 48 | `CQRS.Marten` | ✅ | Generic Marten Commands, Queries, Keyset pagination, Specification Queries, Atomic session transaction behavior, and DomainEvents dispatching |
| - | `CQRS.UnitTests` | ✅ | Complete test suite (52 unit tests covering all 12 pipeline behaviors, 20 logical defect review rounds, security, audit, outbox, batching, projections, live push, specification queries, and generic handlers) |

## Phase 4 - EndpointKit

| # | Project | Status | Notes |
|---|---|---|---|
| 49 | `EndpointKit.Core` | ⚡ | 2878 lines |
| 50 | `EndpointKit.Generator` | ⬜ | 1229 lines, zero tests |
| 51 | `EndpointKit.EF` | ⬜ | **5107 lines, zero tests, no TestApp.** Highest risk in the repo |
| 52 | `EndpointKit.Marten` | ⚡ | 5189 lines. `(dynamic)` in `SendCommandAsync` fixed to `(object)` for 100% Native AOT & trimming safety |

## Phase 5 - DataProtection

Independent of everything else. Can be done at any point.

| # | Project | Status | Notes |
|---|---|---|---|
| 53 | `DataProtection.Abstractions` | ⚡ | |
| 54 | `DataProtection.Runtime` | ⚡ | 1254 lines |
| 55 | `DataProtection.Ephemeral` | ⬜ | 17 lines |
| 56 | `DataProtection.FileSystem` | ⬜ | 28 lines |
| 57 | `DataProtection.CustomXml` | ⬜ | 42 lines |
| 58 | `DataProtection.EntityFramework` | ⬜ | 51 lines |
| 59 | `DataProtection.Redis` | ⚡ | |
| 60 | `DataProtection.Marten` | ⬜ | |
| 61 | `DataProtection.AzureStorage` | ⬜ | Cloud - wiring test only, no integration |
| 62 | `DataProtection.AzureKeyVault` | ⬜ | Cloud - wiring test only |
| 63 | `DataProtection.AwsKms` | ⬜ | Cloud - wiring test only |
| 64 | `DataProtection.GoogleKms` | ⬜ | Cloud - wiring test only |
| 65 | `DataProtection.Cli` | ⬜ | 629 lines |

## Phase 6 - Standalone

Nothing depends on these and they depend on nothing. Any time.

| # | Project | Status | Notes |
|---|---|---|---|
| 66 | `OpenApi` | ⬜ | Migrated to official Microsoft.AspNetCore.OpenApi (.NET 10) + Scalar UI + Swagger UI, covered by integration suite |
| 67 | `OpenIddictAuth` | ⬜ | 181 lines |
| 68 | `IRabbitMQUtilsInterfaces` | 🚫 | 32 lines, contracts only |
| 69 | `RabbitMQUtils` | ⬜ | 218 lines |
| 70 | `Elasticsearch` | ⚡ | Modern Elasticsearch client (v8.17) with repository, fluent search, auto-index lifecycle, and health checks |
| 71 | `Resilience` | ⚡ | Enterprise Polly v8 & Microsoft.Extensions.Resilience with smart IsTransient evaluation, circuit breaker, and HttpClient extensions |

---

## What "reviewed" means

A project is ✅ when all of the following hold:

1. Every file read, not skimmed
2. Defects found are either fixed or written down here
3. Each fix has a test that **fails against the old code** - verify this, do not assume it
4. Public types carry XML docs that say *why*, not just *what*
5. A suite is registered in `quality-gates.json` so CI runs it
6. `python scripts/update-docs.py` regenerated and committed

## What to look for

The codebase is largely AI-generated. These patterns keep recurring:

- Caches keyed on too little - found twice in the mediator, once in the generator
- A `List<T>` written from parallel code
- Unstable sorting where order is meant to be guaranteed
- Handlers or interfaces that stop at the first match instead of taking all of them
- Tests asserting a branch that does not exist, so they can never pass
- `async` methods with no `await`, or `.Result` / `.Wait()` buried in them
- `CancellationToken` accepted as a parameter and then not passed on
- Duplication between the EF and Marten sides that belongs in a shared place

## Commands

```bash
# One suite
dotnet test Tests/UnitTests/<suite>.UnitTests/<suite>.UnitTests.csproj

# One class or one test
dotnet test <project> --filter "FullyQualifiedName~ClassName"

# Coverage for one suite -> TestResults/coverage-run/Coverage/index.html
bash scripts/dotnet-coverage.sh -t "Tests/<suite>/<suite>.csproj"

# Everything CI runs (needs PostgreSQL and Redis)
bash scripts/library-quality.sh --all-suites

# After touching quality-gates.json
python scripts/update-docs.py
```

## Carried-forward findings

Things found while reviewing something else. Address them when their project comes up.

| Project | Finding |
|---|---|
| `Repositories.EF.Abstractions` | Fixed: Unparseable route key throws `FormatException` (yielding 400 instead of 500). Pinned by `ConvertToType_UnparseableGuid_Throws` |
| `EndpointKit.EF` | Fixed: `(dynamic)` in `SendCommandAsync` replaced with `(object)` for Native AOT and trimming safety |
| `EndpointKit.Marten` | Fixed: `(dynamic)` in `SendCommandAsync` replaced with `(object)` for Native AOT and trimming safety |
| `Mediator.Generator` | Fixed: `Microsoft.CodeAnalysis.CSharp` has `PrivateAssets="all"`; `<IsRoslynComponent>true</IsRoslynComponent>` present |
| `Logging.Serilog` | Fixed: Clean XML docs with zero build warnings |
| `Repositories.Marten.Runtime` | Fixed: Clean XML documentation with zero build warnings |
