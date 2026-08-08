using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.ExceptionHandling;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Patch;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Enum;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Envelope;
using KyrolusSous.EndpointKit.Core.FieldSelection;
using KyrolusSous.EndpointKit.Core.Hateoas;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;
using KyrolusSous.ExceptionHandling;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.ClasesAndHelpers;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Handlers;
using KyrolusSous.ExceptionHandling.Interfaces;
using KyrolusSous.ExceptionHandling.Mapping;
using KyrolusSous.ExceptionHandling.Writers;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Repositories.Marten.Abstractions.Authorization;
using KyrolusSous.Repositories.Marten.Abstractions;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Observer;
using KyrolusSous.Repositories.Marten.Abstractions.Query;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using KyrolusSous.Repositories.Marten.Abstractions.Resilience;
using KyrolusSous.Repositories.Marten.Abstractions.SoftDelete;
using KyrolusSous.Repositories.Marten.Abstractions.Specifications;
using KyrolusSous.Repositories.Marten.Abstractions.Tracing;
using KyrolusSous.Repositories.Marten.Abstractions.Validation;
using KyrolusSous.Repositories.Marten.Runtime;
using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using KyrolusSous.Repositories.Marten.Runtime.Projection;
using KyrolusSous.Repositories.Marten.Runtime.Repository;
using KyrolusSous.Repositories.Marten.Runtime.Repository.Decorators;
using KyrolusSous.Repositories.Marten.Runtime.Saga;
using KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;
using KyrolusSous.Validation.Abstractions;
using KyrolusSous.Validation.FluentValidation;
using KyrolusSous.Validation.Runtime;
using KyrolusSous.CQRS.Validation;
using FluentValidation;
using FluentValidation.Results;
using Marten;
using Marten.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Npgsql;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;

public static partial class RepositoryRuntimeDiagnostics
{
    private static async Task<int> RunValidationRuntimeScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusValidationEngine(null!));
        checks++;

        var scanningOnlyServices = new ServiceCollection();
        scanningOnlyServices.AddKyrolusValidationRuntimeScanning();
        checks++;

        var services = new ServiceCollection();
        services.AddKyrolusValidationRuntime();
        services.AddKyrolusValidationProfile(new KyrolusValidationProfile(
            "strict",
            new KyrolusValidationContext(
                RuleSets: ["strict"],
                Groups: ["api"],
                MinimumSeverity: KyrolusValidationSeverity.Warning)));
        services.AddKyrolusValidationProfile(new KyrolusValidationProfile(
            "rules-only",
            new KyrolusValidationContext(
                RuleSets: ["default"],
                Groups: ["default"])));
        services.AddKyrolusValidationRuntimeScanning(typeof(RuntimeScannedValidationRequestValidator).Assembly);

        services.AddSingleton<IKyrolusValidationErrorLocalizer>(new KyrolusDictionaryValidationErrorLocalizer(
            new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["en-US"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["price.invalid"] = "Price is invalid.",
                    ["name.required"] = "Name is required.",
                    ["scanned.invalid"] = "Scanned validator failure."
                }
            },
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["price.invalid"] = "Invariant price validation.",
                ["name.required"] = "Invariant name validation.",
                ["scanned.invalid"] = "Invariant scanned validation."
            }));

        services.AddSingleton<IKyrolusValidationErrorCodeMapper>(new KyrolusDictionaryValidationErrorCodeMapper(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["price"] = "VAL_PRICE",
                ["name.required"] = "VAL_NAME_REQUIRED"
            }));

        services.AddSingleton<IKyrolusValidationFieldPathMapper>(new KyrolusDictionaryValidationFieldPathMapper(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Price"] = "payload.price",
                ["Name"] = "payload.name"
            }));

        services.AddSingleton<RuntimeValidationHook>();
        services.AddSingleton<IKyrolusValidationHook>(sp => sp.GetRequiredService<RuntimeValidationHook>());
        services.AddSingleton<RuntimeTypedValidationHook>();
        services.AddSingleton<IKyrolusValidationHook<RuntimeValidationProbeRequest>>(sp => sp.GetRequiredService<RuntimeTypedValidationHook>());
        services.AddSingleton<IKyrolusRequestValidator<RuntimeValidationProbeRequest>, RuntimeValidationProbeRequestValidator>();
        services.AddSingleton<IKyrolusRequestValidator<RuntimeValidationProbeRequest>, RuntimeValidationProbeContextValidator>();

        using var serviceProvider = services.BuildServiceProvider();
        var runtimeCacheStore = serviceProvider.GetRequiredService<IKyrolusValidationCacheStore>();
        var engine = new KyrolusValidationEngine(
            serviceProvider,
            serviceProvider.GetRequiredService<IKyrolusValidationErrorLocalizer>(),
            runtimeCacheStore,
            serviceProvider.GetRequiredService<IKyrolusValidationCacheKeyProvider>(),
            serviceProvider.GetRequiredService<IKyrolusValidationErrorCodeMapper>(),
            serviceProvider.GetRequiredService<IKyrolusValidationFieldPathMapper>());

        using var providerLessServices = new ServiceCollection()
            .AddSingleton<IKyrolusRequestValidator<RuntimeValidationProbeRequest>, RuntimeValidationProbeRequestValidator>()
            .BuildServiceProvider();
        var providerLessEngine = new KyrolusValidationEngine(providerLessServices);

        var invalidRequest = new RuntimeValidationProbeRequest
        {
            Price = -10,
            Name = string.Empty,
            CacheKey = $"validation:invalid:{Guid.NewGuid():N}",
            CacheMode = KyrolusValidationCacheMode.All,
            CacheTtl = TimeSpan.FromMinutes(1),
            NegativeCacheTtl = TimeSpan.FromSeconds(2)
        };

        var firstFailures = await engine.ValidateAsync(invalidRequest, cancellationToken).ConfigureAwait(false);
        if (firstFailures.Count >= 2 &&
            firstFailures.Any(failure => failure.ErrorCode == "VAL_PRICE" && failure.FieldPath == "payload.price") &&
            firstFailures.Any(failure => failure.ErrorCode == "VAL_NAME_REQUIRED" && failure.FieldPath == "payload.name"))
        {
            checks++;
        }

        var cachedFailures = await engine.ValidateAsync(invalidRequest, cancellationToken).ConfigureAwait(false);
        if (cachedFailures.Count == firstFailures.Count &&
            cachedFailures.All(failure => !string.IsNullOrWhiteSpace(failure.ErrorMessage)))
        {
            checks++;
        }

        var strictFailures = await engine.ValidateAsync(
            invalidRequest,
            new KyrolusValidationContext(Profiles: ["strict"]),
            cancellationToken).ConfigureAwait(false);
        if (strictFailures.Count == 1 &&
            strictFailures[0].PropertyName == "Name" &&
            strictFailures[0].Severity == KyrolusValidationSeverity.Warning)
        {
            checks++;
        }

        var providerLessFailures = await providerLessEngine.ValidateAsync(
            new RuntimeValidationProbeRequest
            {
                Price = -10,
                Name = string.Empty
            },
            new KyrolusValidationContext(Profiles: ["strict"]),
            cancellationToken).ConfigureAwait(false);
        if (providerLessFailures.Count == 2 &&
            providerLessFailures.Any(failure => failure.PropertyName == "Price") &&
            providerLessFailures.Any(failure => failure.PropertyName == "Name"))
        {
            checks++;
        }

        var mergedProfileFailures = await engine.ValidateAsync(
            invalidRequest,
            new KyrolusValidationContext(
                RuleSets: ["default"],
                Groups: ["default"],
                MinimumSeverity: KyrolusValidationSeverity.Info,
                Profiles: ["strict"]),
            cancellationToken).ConfigureAwait(false);
        if (mergedProfileFailures.Count == 2 &&
            mergedProfileFailures.Any(failure => failure.PropertyName == "Price" && failure.Severity == KyrolusValidationSeverity.Error) &&
            mergedProfileFailures.Any(failure => failure.PropertyName == "Name" && failure.Severity == KyrolusValidationSeverity.Warning))
        {
            checks++;
        }

        var rulesOnlyFailures = await engine.ValidateAsync(
            invalidRequest,
            new KyrolusValidationContext(
                MinimumSeverity: KyrolusValidationSeverity.Warning,
                Profiles: ["rules-only"]),
            cancellationToken).ConfigureAwait(false);
        if (rulesOnlyFailures.Count == 1 &&
            rulesOnlyFailures[0].PropertyName == "Price" &&
            rulesOnlyFailures[0].Severity == KyrolusValidationSeverity.Error)
        {
            checks++;
        }

        var filteredFailures = await engine.ValidateAsync(
            invalidRequest,
            new KyrolusValidationContext(
                RuleSets: ["default"],
                Groups: ["default"],
                MinimumSeverity: KyrolusValidationSeverity.Error),
            cancellationToken).ConfigureAwait(false);
        if (filteredFailures.Count == 1 &&
            filteredFailures[0].PropertyName == "Price")
        {
            checks++;
        }

        var noValidatorRequest = new RuntimeNoValidatorValidationProbeRequest
        {
            CacheKey = $"validation:no-validator:{Guid.NewGuid():N}",
            CacheMode = KyrolusValidationCacheMode.SuccessOnly,
            CacheTtl = TimeSpan.FromMinutes(1),
            NegativeCacheTtl = TimeSpan.FromMilliseconds(50)
        };
        var noValidatorFirst = await engine.ValidateAsync(noValidatorRequest, cancellationToken).ConfigureAwait(false);
        var noValidatorSecond = await engine.ValidateAsync(noValidatorRequest, cancellationToken).ConfigureAwait(false);
        if (noValidatorFirst.Count == 0 && noValidatorSecond.Count == 0)
        {
            checks++;
        }

        var nullRequestFailures = await engine.ValidateAsync<RuntimeNoValidatorValidationProbeRequest?>(
            null,
            KyrolusValidationContext.Default,
            cancellationToken).ConfigureAwait(false);
        if (nullRequestFailures.Count == 0)
        {
            checks++;
        }

        var failuresOnlyRequest = new RuntimeValidationProbeRequest
        {
            Price = -1,
            Name = string.Empty,
            CacheKey = $"validation:failures-only:{Guid.NewGuid():N}",
            CacheMode = KyrolusValidationCacheMode.FailuresOnly,
            CacheTtl = TimeSpan.FromMinutes(1),
            NegativeCacheTtl = TimeSpan.FromSeconds(1)
        };
        var failuresOnlyFailures = await engine.ValidateAsync(failuresOnlyRequest, cancellationToken).ConfigureAwait(false);
        if (runtimeCacheStore.TryGet(failuresOnlyRequest.CacheKey!, out var cachedFailuresOnly) &&
            cachedFailuresOnly.Count == failuresOnlyFailures.Count)
        {
            checks++;
        }

        var invalidModeRequest = new RuntimeNoValidatorValidationProbeRequest
        {
            CacheKey = $"validation:invalid-mode:{Guid.NewGuid():N}",
            CacheMode = (KyrolusValidationCacheMode)999,
            CacheTtl = TimeSpan.FromMinutes(1),
            NegativeCacheTtl = TimeSpan.Zero
        };
        var invalidModeFailures = await engine.ValidateAsync(invalidModeRequest, cancellationToken).ConfigureAwait(false);
        if (invalidModeFailures.Count == 0 &&
            !runtimeCacheStore.TryGet(invalidModeRequest.CacheKey!, out _))
        {
            checks++;
        }

        var scannedFailures = await engine.ValidateAsync(new RuntimeScannedValidationRequest(), cancellationToken).ConfigureAwait(false);
        if (scannedFailures.Count == 1 &&
            scannedFailures[0].MessageKey == "scanned.invalid")
        {
            checks++;
        }

        var composite2 = await engine.ValidateCompositeAsync("a", 2, cancellationToken).ConfigureAwait(false);
        var composite2WithContext = await engine.ValidateCompositeAsync("a", 2, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);
        var composite3 = await engine.ValidateCompositeAsync("a", 2, true, cancellationToken).ConfigureAwait(false);
        var composite3WithContext = await engine.ValidateCompositeAsync("a", 2, true, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);
        var composite4 = await engine.ValidateCompositeAsync("a", 2, true, 4.0m, cancellationToken).ConfigureAwait(false);
        var composite4WithContext = await engine.ValidateCompositeAsync("a", 2, true, 4.0m, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);
        if (composite2.Count == 0 &&
            composite2WithContext.Count == 0 &&
            composite3.Count == 0 &&
            composite3WithContext.Count == 0 &&
            composite4.Count == 0 &&
            composite4WithContext.Count == 0)
        {
            checks++;
        }

        var hook = serviceProvider.GetRequiredService<RuntimeValidationHook>();
        var typedHook = serviceProvider.GetRequiredService<RuntimeTypedValidationHook>();
        if (hook.BeforeCount >= 5 &&
            hook.AfterCount >= 5 &&
            typedHook.BeforeCount >= 3 &&
            typedHook.AfterCount >= 3)
        {
            checks++;
        }

        var profileProvider = serviceProvider.GetRequiredService<IKyrolusValidationProfileProvider>();
        if (profileProvider.TryGetProfile("strict", out var strictProfileContext) &&
            strictProfileContext.RuleSets is { Count: > 0 } &&
            strictProfileContext.Groups is { Count: > 0 } &&
            !profileProvider.TryGetProfile(string.Empty, out _))
        {
            checks++;
        }

        var nullLocalizer = new KyrolusNullValidationErrorLocalizer();
        if (nullLocalizer.Localize(new KyrolusValidationFailure("Name", "raw-message")) == "raw-message")
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusDelegateValidationErrorCodeMapper(null!));
        checks++;

        var delegateCodeMapper = new KyrolusDelegateValidationErrorCodeMapper((failure, _) => $"CODE::{failure.PropertyName}");
        if (delegateCodeMapper.MapErrorCode(new KyrolusValidationFailure("Name", "msg"), KyrolusValidationContext.Default) == "CODE::Name")
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusDelegateValidationFieldPathMapper(null!));
        checks++;

        var delegatePathMapper = new KyrolusDelegateValidationFieldPathMapper((failure, _) => $"path.{failure.PropertyName}");
        if (delegatePathMapper.MapFieldPath(new KyrolusValidationFailure("Name", "msg"), KyrolusValidationContext.Default) == "path.Name")
        {
            checks++;
        }

        var dictionaryCodeMapper = new KyrolusDictionaryValidationErrorCodeMapper(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "DICT_NAME"
            });
        if (dictionaryCodeMapper.MapErrorCode(new KyrolusValidationFailure("Name", "msg"), KyrolusValidationContext.Default) == "DICT_NAME")
        {
            checks++;
        }

        var dictionaryPathMapper = new KyrolusDictionaryValidationFieldPathMapper(
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "dictionary.name"
            });
        if (dictionaryPathMapper.MapFieldPath(new KyrolusValidationFailure("Name", "msg"), KyrolusValidationContext.Default) == "dictionary.name")
        {
            checks++;
        }

        var cacheKeyProvider = new KyrolusValidationCacheKeyProvider();
        if (cacheKeyProvider.GetCacheEntry(new object(), KyrolusValidationContext.Default) is null &&
            cacheKeyProvider.GetCacheEntry(new RuntimeValidationProbeRequest
            {
                Price = 1,
                Name = "ok",
                CacheKey = "cache-key-provider",
                CacheMode = KyrolusValidationCacheMode.All,
                CacheTtl = TimeSpan.FromSeconds(30)
            }, KyrolusValidationContext.Default) is not null &&
            cacheKeyProvider.GetCacheEntry(new RuntimeValidationProbeRequest
            {
                Price = 1,
                Name = "ok",
                CacheKey = "cache-key-provider-none",
                CacheMode = KyrolusValidationCacheMode.None,
                CacheTtl = TimeSpan.FromSeconds(30)
            }, KyrolusValidationContext.Default) is not null &&
            cacheKeyProvider.GetCacheEntry(new RuntimeValidationProbeRequest
            {
                Price = 1,
                Name = "ok",
                CacheKey = "cache-key-provider-invalid-ttl",
                CacheMode = KyrolusValidationCacheMode.All,
                CacheTtl = TimeSpan.Zero
            }, KyrolusValidationContext.Default) is null)
        {
            checks++;
        }

        var directCacheStore = new KyrolusValidationMemoryCacheStore();
        directCacheStore.Set("validation-cache", firstFailures, TimeSpan.FromMilliseconds(50));
        if (directCacheStore.TryGet("validation-cache", out var directCacheHit) && directCacheHit.Count == firstFailures.Count)
        {
            checks++;
        }

        await Task.Delay(55, cancellationToken).ConfigureAwait(false);
        if (!directCacheStore.TryGet("validation-cache", out _) &&
            !directCacheStore.TryGet(string.Empty, out _))
        {
            checks++;
        }

        if (KyrolusValidationProfiles.All.Count == 4 &&
            KyrolusValidationProfiles.All.Select(profile => profile.Name).SequenceEqual(["Create", "Update", "UiHints", "BackgroundJobs"]) &&
            KyrolusValidationProfiles.Create.Context.RuleSets!.Single() == "Create" &&
            KyrolusValidationProfiles.Update.Context.RuleSets!.Single() == "Update" &&
            KyrolusValidationProfiles.UiHints.Context.Groups!.Single() == "UiHints" &&
            KyrolusValidationProfiles.BackgroundJobs.Context.RuleSets!.Single() == "BackgroundJobs" &&
            KyrolusValidationProfiles.UiHints.Context.MinimumSeverity == KyrolusValidationSeverity.Info)
        {
            checks++;
        }

        Dictionary<string, object?>? activityTags = null;
        ActivityStatusCode? activityStatus = null;
        string? activityStatusDescription = null;
        using var activityListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Kyrolus.Validation.Diagnostics",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            SampleUsingParentId = static (ref ActivityCreationOptions<string> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                activityTags = activity.TagObjects.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);
                activityStatus = activity.Status;
                activityStatusDescription = activity.StatusDescription;
            }
        };
        ActivitySource.AddActivityListener(activityListener);

        var tracer = new KyrolusValidationActivityTracer("Kyrolus.Validation.Diagnostics");
        var traceContext = new KyrolusValidationTraceContext(
            typeof(RuntimeValidationProbeRequest),
            new KyrolusValidationContext(
                RuleSets: ["strict"],
                Groups: ["api"],
                MinimumSeverity: KyrolusValidationSeverity.Warning));
        var traceState = tracer.Start(traceContext);
        await tracer.StopAsync(traceContext, traceState, firstFailures, new InvalidOperationException("validation-trace"), cancellationToken).ConfigureAwait(false);
        if (traceState is Activity &&
            activityTags is not null &&
            Equals(activityTags["validation.request_type"], typeof(RuntimeValidationProbeRequest).FullName) &&
            Equals(activityTags["validation.rule_sets"], "strict") &&
            Equals(activityTags["validation.groups"], "api") &&
            Equals(activityTags["validation.min_severity"], KyrolusValidationSeverity.Warning.ToString()) &&
            Equals(activityTags["validation.failures"], firstFailures.Count) &&
            Equals(activityTags["validation.max_severity"], firstFailures.Max(failure => failure.Severity).ToString()) &&
            Equals(activityTags["validation.exception"], typeof(InvalidOperationException).FullName) &&
            activityStatus == ActivityStatusCode.Error &&
            activityStatusDescription == "validation-trace")
        {
            checks++;
        }

        var noopTracer = KyrolusNoopValidationTracer.Instance;
        var noopState = noopTracer.Start(traceContext);
        await noopTracer.StopAsync(traceContext, noopState, firstFailures, null, cancellationToken).ConfigureAwait(false);
        checks++;

        var metricsCount = 0;
        var metrics = new KyrolusDelegateValidationMetrics((_, _) =>
        {
            metricsCount++;
            return ValueTask.CompletedTask;
        });
        var metricsHook = new KyrolusValidationMetricsHook(metrics);
        await metricsHook.OnBeforeAsync(invalidRequest, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);
        await metricsHook.OnAfterAsync(invalidRequest, KyrolusValidationContext.Default, firstFailures, cancellationToken).ConfigureAwait(false);
        if (metricsCount == 1)
        {
            checks++;
        }

        var tracingHook = new KyrolusValidationTracingHook(noopTracer);
        await tracingHook.OnBeforeAsync(invalidRequest, KyrolusValidationContext.Default, cancellationToken).ConfigureAwait(false);
        await tracingHook.OnAfterAsync(invalidRequest, KyrolusValidationContext.Default, firstFailures, cancellationToken).ConfigureAwait(false);
        checks++;

        var noopMetrics = KyrolusNoopValidationMetrics.Instance;
        await noopMetrics.RecordAsync(new KyrolusValidationMetricsContext(typeof(RuntimeValidationProbeRequest), KyrolusValidationContext.Default, firstFailures, TimeSpan.FromMilliseconds(1)), cancellationToken).ConfigureAwait(false);
        checks++;

        checks += await RunFluentValidationScenariosAsync(cancellationToken).ConfigureAwait(false);

        return checks;
    }

}
