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
| 19 | `Compression` | 🔧 | 6 compression algorithms (Brotli, Zstd, LZ4, Snappy, Gzip, Deflate), stream support, byte/string extensions, and ASP.NET Core ResponseCompressionMiddleware |
| 20 | `Caching.Abstractions` | 🔧 | Core caching abstractions, IDistributedLockProvider, IKyrolusRedisPubSub, atomic/hash operations, payload transformers |
| 21 | `Caching.Redis` | 🔧 | Redis L2 & NearCache (L1+L2), Lua-based distributed locking, Pub/Sub bus, ASP.NET Core IDistributedCache & IOutputCacheStore adapters |
| 22 | `Caching.MessagePack` | 🔧 | High-performance binary MessagePack cache serializer with optional LZ4 block compression |
| 23 | `Mapping.Abstractions` | 🚫 | 8 lines |
| 24 | `Mapping.Mapster` | ⬜ | 30 lines |
| 25 | `Logging.Abstractions` | 🚫 | 23 lines |
| 26 | `Logging.Runtime` | ⬜ | 79 lines |
| 27 | `Logging.Serilog` | ⬜ | 28 tests |

## Phase 2 - Repositories

| # | Project | Status | Notes |
|---|---|---|---|
| 28 | `Repositories.EF.Abstractions` | ⬜ | Gate 65%/55%. Unparseable route keys fixed to throw FormatException (yielding 400 instead of 500) |
| 29 | `Repositories.EF.Runtime` | ⬜ | 475 integration + 106 unit. Gate 95%/85% |
| 30 | `Repositories.EF.Generator` | ⬜ | 133 integration + 67 unit |
| 31 | `Repositories.EF.Cache.Distributed` | ⬜ | 220 lines |
| 32 | `Repositories.Marten.Abstractions` | ⚡ | 2927 lines |
| 33 | `Repositories.Marten.Runtime` | ⬜ | 235 tests via FullPipeline |
| 34 | `Repositories.Marten.Generator` | ⬜ | **2950 lines, zero tests.** Compare against the EF generator, which is tested |

## Phase 3 - CQRS

| # | Project | Status | Notes |
|---|---|---|---|
| 35 | `CQRS.Abstractions` | ⚡ | 27 lines |
| 36 | `CQRS.Validation` | ⚡ | |
| 37 | `CQRS.Mapping` | ⬜ | 23 lines |
| 38 | `CQRS.ExceptionHandling` | ⚡ | |
| 39 | `CQRS.Caching` | ⬜ | Holds the Redis caching behaviour |
| 40 | `CQRS.EF` | ⬜ | **2007 lines, zero tests, no TestApp.** Highest risk in this phase |
| 41 | `CQRS.Marten` | ⚡ | |

## Phase 4 - EndpointKit

| # | Project | Status | Notes |
|---|---|---|---|
| 42 | `EndpointKit.Core` | ⚡ | 2878 lines |
| 43 | `EndpointKit.Generator` | ⬜ | 1229 lines, zero tests |
| 44 | `EndpointKit.EF` | ⬜ | **5107 lines, zero tests, no TestApp.** Highest risk in the repo |
| 45 | `EndpointKit.Marten` | ⚡ | 5189 lines. **Open finding:** `(dynamic)` in `SendCommandAsync` breaks AOT and trimming, contradicting the README's AOT claim |

## Phase 5 - DataProtection

Independent of everything else. Can be done at any point.

| # | Project | Status | Notes |
|---|---|---|---|
| 46 | `DataProtection.Abstractions` | ⚡ | |
| 47 | `DataProtection.Runtime` | ⚡ | 1254 lines |
| 48 | `DataProtection.Ephemeral` | ⬜ | 17 lines |
| 49 | `DataProtection.FileSystem` | ⬜ | 28 lines |
| 50 | `DataProtection.CustomXml` | ⬜ | 42 lines |
| 51 | `DataProtection.EntityFramework` | ⬜ | 51 lines |
| 52 | `DataProtection.Redis` | ⚡ | |
| 53 | `DataProtection.Marten` | ⬜ | |
| 54 | `DataProtection.AzureStorage` | ⬜ | Cloud - wiring test only, no integration |
| 55 | `DataProtection.AzureKeyVault` | ⬜ | Cloud - wiring test only |
| 56 | `DataProtection.AwsKms` | ⬜ | Cloud - wiring test only |
| 57 | `DataProtection.GoogleKms` | ⬜ | Cloud - wiring test only |
| 58 | `DataProtection.Cli` | ⬜ | 629 lines |

## Phase 6 - Standalone

Nothing depends on these and they depend on nothing. Any time.

| # | Project | Status | Notes |
|---|---|---|---|
| 59 | `OpenApi` | ⬜ | Migrated to official Microsoft.AspNetCore.OpenApi (.NET 10) + Scalar UI + Swagger UI, covered by integration suite |
| 60 | `OpenIddictAuth` | ⬜ | 181 lines |
| 61 | `IRabbitMQUtilsInterfaces` | 🚫 | 32 lines, contracts only |
| 62 | `RabbitMQUtils` | ⬜ | 218 lines |
| 63 | `Elasticsearch` | ⚡ | Modern Elasticsearch client (v8.17) with repository, fluent search, auto-index lifecycle, and health checks |
| 64 | `Resilience` | ⚡ | Enterprise Polly v8 & Microsoft.Extensions.Resilience with smart IsTransient evaluation, circuit breaker, and HttpClient extensions |

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
| `Repositories.EF.Abstractions` | Unparseable route key throws `InvalidCastException`, so a bad URL is a 500 rather than a 400. Pinned by `ConvertToType_UnparseableGuid_Throws` |
| `EndpointKit.EF` | `(dynamic)` in `SendCommandAsync` (~line 2695) breaks AOT and trimming |
| `Mediator.Generator` | `Microsoft.CodeAnalysis.CSharp` has no `PrivateAssets="all"`, so it leaks to consumers as a dependency |
| `Mediator.Generator` | `<IsRoslynComponent>true</IsRoslynComponent>` missing - would enable the Roslyn Component debug profile |
| `Mediator.Generator` | The generated code's open-generic fallback still uses reflection, so the AOT claim holds only when no open-generic handlers are used |
| `Logging.Serilog` | Malformed XML doc at `LoggingOptions.cs:152` - 3 build warnings |
| `Repositories.Marten.Runtime` | Unresolvable `cref` at `KyrolusMartenDaemonOptions.cs:9` - 1 build warning |
