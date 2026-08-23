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
using KyrolusSous.ExceptionHandling.Runtime;
using KyrolusSous.ExceptionHandling.Abstractions.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.ExceptionHandling.Runtime.Helpers;
using KyrolusSous.ExceptionHandling.FluentValidation;
using KyrolusSous.ExceptionHandling.Runtime.Handlers;
using KyrolusSous.ExceptionHandling.Runtime.Interfaces;
using KyrolusSous.ExceptionHandling.Runtime.Mapping;
using KyrolusSous.ExceptionHandling.Runtime.Writers;
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
    private static async Task<int> RunCacheAbstractionsScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        var defaultPolicy = new KyrolusCachePolicy(AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(5), KeySuffix: "default");
        var operationPolicy = new KyrolusCachePolicy(SlidingExpiration: TimeSpan.FromMinutes(2), KeySuffix: "operation");
        var typePolicy = new KyrolusCachePolicy(NegativeCacheTtl: TimeSpan.FromSeconds(30), KeySuffix: "type");
        var cachePolicyRegistry = new KyrolusCachePolicyRegistry()
            .SetDefault(defaultPolicy)
            .SetForOperation(KyrolusCacheOperation.Get, operationPolicy)
            .SetForType<RuntimeCachePayload>(KyrolusCacheOperation.Get, typePolicy);

        if (cachePolicyRegistry.GetPolicy(typeof(RuntimeCachePayload), KyrolusCacheOperation.Get)?.KeySuffix == "type" &&
            cachePolicyRegistry.GetPolicy(typeof(string), KyrolusCacheOperation.Get)?.KeySuffix == "operation" &&
            ReferenceEquals(cachePolicyRegistry.GetPolicy(typeof(int), KyrolusCacheOperation.Set), defaultPolicy) &&
            KyrolusNullCachePolicyProvider.Instance.GetPolicy(typeof(RuntimeCachePayload), KyrolusCacheOperation.Remove) is null)
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusCachePolicyRegistry().SetDefault(null!));
        checks++;

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusCachePolicyRegistry().SetForOperation(KyrolusCacheOperation.Get, null!));
        checks++;

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusCachePolicyRegistry().SetForType<RuntimeCachePayload>(KyrolusCacheOperation.Get, null!));
        checks++;

        var repositoryDefault = new KyrolusCachePolicy(KeySuffix: "repo-default");
        var repositoryOperation = new KyrolusCachePolicy(KeySuffix: "repo-operation");
        var repositoryType = new KyrolusCachePolicy(KeySuffix: "repo-type");
        var repositoryTenant = new KyrolusCachePolicy(KeySuffix: "repo-tenant");
        var repositoryTenantOperation = new KyrolusCachePolicy(KeySuffix: "repo-tenant-operation");
        var repositoryTenantType = new KyrolusCachePolicy(KeySuffix: "repo-tenant-type");
        var repositoryRegistry = new KyrolusRepositoryCachePolicyRegistry()
            .SetDefault(repositoryDefault)
            .SetForOperation("get", repositoryOperation)
            .SetForType<RuntimeCachePayload>("get", repositoryType)
            .SetForTenant("tenant-a", repositoryTenant)
            .SetForTenantOperation("tenant-a", "get", repositoryTenantOperation)
            .SetForTenantType<RuntimeCachePayload>("tenant-a", "get", repositoryTenantType);

        if ((await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(RuntimeCachePayload), Operation: "get", TenantId: "tenant-a"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-tenant-type" &&
            (await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "get", TenantId: "tenant-a"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-tenant-operation" &&
            (await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "set", TenantId: "tenant-a"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-tenant" &&
            (await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(RuntimeCachePayload), Operation: "get"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-type" &&
            (await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "get"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-operation" &&
            (await repositoryRegistry.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "set"), cancellationToken).ConfigureAwait(false))?.KeySuffix == "repo-default" &&
            await KyrolusNoopRepositoryCachePolicyProvider.Instance.GetPolicyAsync(new KyrolusRepositoryCachePolicyContext(typeof(string), Operation: "set"), cancellationToken).ConfigureAwait(false) is null)
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusRepositoryCachePolicyRegistry().SetDefault(null!));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusRepositoryCachePolicyRegistry().SetForOperation(" ", repositoryOperation));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusRepositoryCachePolicyRegistry().SetForTenant(" ", repositoryTenant));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusRepositoryCachePolicyRegistry().SetForTenantOperation("tenant", " ", repositoryTenantOperation));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusRepositoryCachePolicyRegistry().SetForTenantType<RuntimeCachePayload>(" ", "get", repositoryTenantType));
        checks++;

        ExpectThrows<ArgumentNullException>(
            () => _ = new KyrolusRepositoryCachePolicyRegistry().GetPolicyAsync(null!, cancellationToken).GetAwaiter().GetResult());
        checks++;

        var nullCacheProvider = NullCacheProvider.Instance;
        await nullCacheProvider.SetAsync("set", new RuntimeCachePayload { Name = "value", Count = 1 }, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.SetAsync("set-options", new RuntimeCachePayload { Name = "value", Count = 1 }, new KyrolusCacheEntryOptions(), cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.RemoveAsync("set", cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.RemoveKeysByPatternAsync("pattern*", cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.SetManyAsync(new[]
        {
            new KeyValuePair<string, RuntimeCachePayload>("one", new RuntimeCachePayload { Name = "one", Count = 1 })
        }, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.SetManyAsync(new[]
        {
            new KeyValuePair<string, RuntimeCachePayload>("one", new RuntimeCachePayload { Name = "one", Count = 1 })
        }, new KyrolusCacheEntryOptions(), cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.RemoveManyAsync(["one"], cancellationToken).ConfigureAwait(false);
        await nullCacheProvider.RemoveByTagAsync("tag", cancellationToken).ConfigureAwait(false);
        var factoryCalls = 0;
        var nullCacheValue = await nullCacheProvider.GetOrCreateAsync(
            "factory",
            _ =>
            {
                factoryCalls++;
                return Task.FromResult(new RuntimeCachePayload { Name = "factory", Count = 2 });
            },
            cancellationToken: cancellationToken).ConfigureAwait(false);
        if (await nullCacheProvider.GetAsync<RuntimeCachePayload>("missing", cancellationToken).ConfigureAwait(false) is null &&
            !await nullCacheProvider.ExistsAsync("missing", cancellationToken).ConfigureAwait(false) &&
            (await nullCacheProvider.GetManyAsync<RuntimeCachePayload>(["one", "two"], cancellationToken).ConfigureAwait(false)).Count == 0 &&
            nullCacheValue.Name == "factory" &&
            factoryCalls == 1)
        {
            checks++;
        }

        var observerContext = new KyrolusCacheObserverContext(
            Key: "cache-key",
            Operation: KyrolusCacheOperation.Get,
            Observation: KyrolusCacheObservation.Hit,
            ValueType: typeof(RuntimeCachePayload),
            Duration: TimeSpan.FromMilliseconds(5),
            Region: "runtime",
            TenantId: "tenant-a",
            Exception: null);
        await KyrolusNullCacheObserver.Instance.OnObservationAsync(observerContext).ConfigureAwait(false);
        if (observerContext.Operation == KyrolusCacheOperation.Get &&
            observerContext.Observation == KyrolusCacheObservation.Hit &&
            observerContext.ValueType == typeof(RuntimeCachePayload))
        {
            checks++;
        }

        var key = Encoding.UTF8.GetBytes("0123456789ABCDEF0123456789ABCDEF");
        var iv = Encoding.UTF8.GetBytes("1234567890ABCDEF");
        var payload = Encoding.UTF8.GetBytes("cache-payload");
        var aesWithStaticIv = new KyrolusAesCachePayloadTransformer(key, iv);
        var aesStaticEncrypted = aesWithStaticIv.Transform(payload);
        var aesStaticRestored = aesWithStaticIv.Restore(aesStaticEncrypted);
        var aesWithDynamicIv = new KyrolusAesCachePayloadTransformer(key);
        var aesDynamicEncrypted = aesWithDynamicIv.Transform(payload);
        var aesDynamicRestored = aesWithDynamicIv.Restore(aesDynamicEncrypted);
        if (Encoding.UTF8.GetString(aesStaticRestored) == "cache-payload" &&
            Encoding.UTF8.GetString(aesDynamicRestored) == "cache-payload" &&
            aesDynamicEncrypted.Length > payload.Length)
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusAesCachePayloadTransformer(null!));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusAesCachePayloadTransformer([1, 2, 3]));
        checks++;

        ExpectThrows<ArgumentException>(() => _ = new KyrolusAesCachePayloadTransformer(key, [1, 2, 3]));
        checks++;

        ExpectThrows<InvalidOperationException>(() => _ = aesWithDynamicIv.Restore([1, 2, 3]));
        checks++;

        var gzipTransformer = new KyrolusGzipCachePayloadTransformer(minSizeBytes: 8);
        var shortPayload = Encoding.UTF8.GetBytes("short");
        var longPayload = Encoding.UTF8.GetBytes(new string('x', 256));
        var rawTransformed = gzipTransformer.Transform(shortPayload);
        var compressedTransformed = gzipTransformer.Transform(longPayload);
        var unknownHeaderPayload = new byte[] { (byte)'K', (byte)'Y', (byte)'C', (byte)'0', 9, 1, 2, 3 };
        if (Encoding.UTF8.GetString(gzipTransformer.Restore(rawTransformed)) == "short" &&
            Encoding.UTF8.GetString(gzipTransformer.Restore(compressedTransformed)) == new string('x', 256) &&
            gzipTransformer.Restore(unknownHeaderPayload).SequenceEqual(unknownHeaderPayload) &&
            gzipTransformer.Restore([(byte)'K', (byte)'Y', (byte)'C', (byte)'0']).Length == 4)
        {
            checks++;
        }

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusJsonContextCacheSerializer(null!));
        checks++;

        var serializer = new KyrolusJsonContextCacheSerializer(RuntimeCacheJsonContext.Default);
        var serializedPayload = serializer.Serialize(new RuntimeCachePayload { Name = "serialized", Count = 3 });
        var deserializedPayload = serializer.Deserialize<RuntimeCachePayload>(serializedPayload);
        if (deserializedPayload?.Name == "serialized" &&
            deserializedPayload.Count == 3)
        {
            checks++;
        }

        ExpectThrows<InvalidOperationException>(() => _ = serializer.Serialize(new RuntimeMissingCachePayload { Value = "missing" }));
        checks++;

        ExpectThrows<InvalidOperationException>(() => _ = serializer.Deserialize<RuntimeMissingCachePayload>(Encoding.UTF8.GetBytes("{}")));
        checks++;

        await Task.Yield();
        return checks;
    }

    private static async Task<int> RunEndpointPolicyFilterScenariosAsync(string tenantId, CancellationToken cancellationToken)
    {
        var checks = 0;
        var endpointKitAssembly = typeof(KyrolusEndpointPolicies).Assembly;
        var idempotencyFilterType = endpointKitAssembly.GetType(
            "KyrolusSous.EndpointKit.Core.BaseKyrolusModule.KyrolusIdempotencyEndpointFilter",
            throwOnError: true)!;
        var outputFilterType = endpointKitAssembly.GetType(
            "KyrolusSous.EndpointKit.Core.BaseKyrolusModule.KyrolusOutputCacheEndpointFilter`1",
            throwOnError: true)!.MakeGenericType(typeof(RuntimeLinkItem));

        var idempotencyOptions = new KyrolusIdempotencyOptions(
            Enabled: true,
            IncludeGet: false,
            HeaderName: "Idempotency-Key",
            Ttl: TimeSpan.FromMinutes(2));

        var idempotencyStore = new KyrolusInMemoryIdempotencyStore();
        var idempotencyFilter = Activator.CreateInstance(
            idempotencyFilterType,
            idempotencyStore,
            idempotencyOptions)!;

        var postContext = new DefaultHttpContext();
        postContext.Request.Method = HttpMethods.Post;
        postContext.Request.Path = "/api/runtime/idempotency";
        postContext.Request.Headers[idempotencyOptions.HeaderName] = "runtime-idempotency-key";

        var postInvocation = new RuntimeEndpointFilterInvocationContext(postContext);
        var postNextCalls = 0;
        EndpointFilterDelegate postNext = _ =>
        {
            postNextCalls++;
            return ValueTask.FromResult<object?>(Results.Json(new { Saved = true }, statusCode: StatusCodes.Status201Created));
        };

        var firstPostResult = await InvokeInternalEndpointFilterAsync(idempotencyFilter, postInvocation, postNext).ConfigureAwait(false);
        var secondPostResult = await InvokeInternalEndpointFilterAsync(idempotencyFilter, postInvocation, postNext).ConfigureAwait(false);
        if (postNextCalls == 1 && firstPostResult is not null && secondPostResult is not null)
        {
            checks++;
        }

        var missingHeaderContext = new DefaultHttpContext();
        missingHeaderContext.Request.Method = HttpMethods.Post;
        missingHeaderContext.Request.Path = "/api/runtime/idempotency";
        var missingHeaderInvocation = new RuntimeEndpointFilterInvocationContext(missingHeaderContext);
        var missingHeaderCalls = 0;
        EndpointFilterDelegate missingHeaderNext = _ =>
        {
            missingHeaderCalls++;
            return ValueTask.FromResult<object?>(Results.Ok(new { PassThrough = true }));
        };
        _ = await InvokeInternalEndpointFilterAsync(idempotencyFilter, missingHeaderInvocation, missingHeaderNext).ConfigureAwait(false);
        if (missingHeaderCalls == 1)
        {
            checks++;
        }

        var getContext = new DefaultHttpContext();
        getContext.Request.Method = HttpMethods.Get;
        getContext.Request.Path = "/api/runtime/idempotency";
        getContext.Request.Headers[idempotencyOptions.HeaderName] = "runtime-idempotency-get";
        var getInvocation = new RuntimeEndpointFilterInvocationContext(getContext);
        var getCalls = 0;
        EndpointFilterDelegate getNext = _ =>
        {
            getCalls++;
            return ValueTask.FromResult<object?>(Results.Ok(new { Get = true }));
        };
        _ = await InvokeInternalEndpointFilterAsync(idempotencyFilter, getInvocation, getNext).ConfigureAwait(false);
        if (getCalls == 1)
        {
            checks++;
        }

        var disabledIdempotencyFilter = Activator.CreateInstance(
            idempotencyFilterType,
            idempotencyStore,
            idempotencyOptions with { Enabled = false, IncludeGet = true })!;
        var disabledCalls = 0;
        EndpointFilterDelegate disabledNext = _ =>
        {
            disabledCalls++;
            return ValueTask.FromResult<object?>(Results.Ok());
        };
        _ = await InvokeInternalEndpointFilterAsync(disabledIdempotencyFilter, getInvocation, disabledNext).ConfigureAwait(false);
        if (disabledCalls == 1)
        {
            checks++;
        }

        var outputCacheRegistry = new KyrolusEndpointCachePolicyRegistry()
            .SetForEntity<RuntimeLinkItem>(EndpointNames.GetAll,
                new KyrolusCachePolicy(Enabled: true, AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(1), KeySuffix: "dynamic"));

        var cacheServices = new ServiceCollection();
        cacheServices.AddLogging();
        cacheServices.AddSingleton<ICacheProvider, RuntimeInMemoryCacheProvider>();
        cacheServices.AddSingleton<ICacheKeyContext>(new RuntimeCacheKeyContext("scope-output", "region-output", tenantId));
        cacheServices.AddSingleton<IKyrolusEndpointCachePolicyProvider>(outputCacheRegistry);
        var cacheProvider = cacheServices.BuildServiceProvider();

        var outputCacheConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeLinkItem",
            Prefix = "api",
            Route = "runtime-link-item",
            ViewModelType = typeof(RuntimeOutputCacheAttributedModel),
            EnableOutputCaching = true,
            OutputCachePolicy = new KyrolusCachePolicy(
                Enabled: true,
                AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(2),
                KeySuffix: "base"),
            EndpointConfig =
            [
                new KyrolusEndpointConfig
                {
                    Name = EndpointNames.GetAll,
                    OutputCacheEnabled = true
                }
            ]
        };

        var outputFilter = Activator.CreateInstance(
            outputFilterType,
            outputCacheConfig,
            EndpointNames.GetAll)!;

        var getCacheContext = new DefaultHttpContext
        {
            RequestServices = cacheProvider
        };
        getCacheContext.Request.Method = HttpMethods.Get;
        getCacheContext.Request.Path = "/api/runtime-link-items";
        getCacheContext.Request.QueryString = new QueryString("?pageNumber=1&pageSize=5");
        getCacheContext.Request.Headers.Accept = "application/json";

        var getCacheInvocation = new RuntimeEndpointFilterInvocationContext(getCacheContext);
        var getCacheCalls = 0;
        EndpointFilterDelegate getCacheNext = _ =>
        {
            getCacheCalls++;
            return ValueTask.FromResult<object?>(Results.Json(new RuntimeLinkItem { Id = Guid.NewGuid(), Name = "Cached" }));
        };
        _ = await InvokeInternalEndpointFilterAsync(outputFilter, getCacheInvocation, getCacheNext).ConfigureAwait(false);
        _ = await InvokeInternalEndpointFilterAsync(outputFilter, getCacheInvocation, getCacheNext).ConfigureAwait(false);
        if (getCacheCalls == 1 &&
            getCacheContext.Response.Headers.CacheControl.ToString().Contains("max-age", StringComparison.OrdinalIgnoreCase))
        {
            checks++;
        }

        var postCacheContext = new DefaultHttpContext
        {
            RequestServices = cacheProvider
        };
        postCacheContext.Request.Method = HttpMethods.Post;
        postCacheContext.Request.Path = "/api/runtime-link-items";
        var postCacheInvocation = new RuntimeEndpointFilterInvocationContext(postCacheContext);
        var postCacheCalls = 0;
        EndpointFilterDelegate postCacheNext = _ =>
        {
            postCacheCalls++;
            return ValueTask.FromResult<object?>(Results.Ok(new RuntimeLinkItem { Id = Guid.NewGuid(), Name = "Post" }));
        };
        _ = await InvokeInternalEndpointFilterAsync(outputFilter, postCacheInvocation, postCacheNext).ConfigureAwait(false);
        if (postCacheCalls == 1)
        {
            checks++;
        }

        var nullCacheServices = new ServiceCollection();
        nullCacheServices.AddSingleton<ICacheProvider>(new NullCacheProvider());
        nullCacheServices.AddSingleton<ICacheKeyContext>(new RuntimeCacheKeyContext(null, null, tenantId));
        nullCacheServices.AddSingleton<IKyrolusEndpointCachePolicyProvider>(KyrolusNoopEndpointCachePolicyProvider.Instance);
        var nullCacheProvider = nullCacheServices.BuildServiceProvider();

        var nullCacheContext = new DefaultHttpContext
        {
            RequestServices = nullCacheProvider
        };
        nullCacheContext.Request.Method = HttpMethods.Get;
        nullCacheContext.Request.Path = "/api/runtime-link-items";

        var nullCacheInvocation = new RuntimeEndpointFilterInvocationContext(nullCacheContext);
        var nullCacheCalls = 0;
        EndpointFilterDelegate nullCacheNext = _ =>
        {
            nullCacheCalls++;
            return ValueTask.FromResult<object?>(Results.Ok(new RuntimeLinkItem { Id = Guid.NewGuid(), Name = "NullCache" }));
        };
        _ = await InvokeInternalEndpointFilterAsync(outputFilter, nullCacheInvocation, nullCacheNext).ConfigureAwait(false);
        if (nullCacheCalls == 1)
        {
            checks++;
        }

        var noopContext = new KyrolusEndpointCachePolicyContext(
            typeof(RuntimeLinkItem),
            nameof(RuntimeLinkItem),
            EndpointNames.GetAll,
            "GET",
            "/api/runtime-link-items",
            tenantId,
            "scope-output");
        var noopPolicy = await KyrolusNoopEndpointCachePolicyProvider.Instance
            .GetPolicyAsync(noopContext, cancellationToken).ConfigureAwait(false);
        if (noopPolicy is null)
        {
            checks++;
        }

        var appBuilder = WebApplication.CreateBuilder();
        appBuilder.Services.AddAuthorization();
        appBuilder.Services.AddSingleton<IKyrolusIdempotencyStore>(idempotencyStore);
        var app = appBuilder.Build();

        var routeBuilder = app.MapPost("/api/runtime/policies", () => Results.Ok());
        var policyConfig = new ApiKyrolusApiConfig<RuntimeLinkItem>
        {
            ApiName = "RuntimeLinkItem",
            Prefix = "api",
            Route = "runtime-link-item",
            EnableIdempotency = true,
            IdempotencyHeaderName = "Idempotency-Key",
            EndpointConfig =
            [
                new KyrolusEndpointConfig
                {
                    Name = EndpointNames.Add,
                    Idempotent = true
                }
            ]
        };
        routeBuilder.ApplyEndpointPolicies(policyConfig, EndpointNames.Add);
        routeBuilder.Authorize((true, null));
        routeBuilder.Authorize((true, "runtime-policy"));
        routeBuilder.Authorize((false, null));
        checks++;

        await Task.Yield();
        return checks;
    }

    private static async Task<object?> InvokeInternalEndpointFilterAsync(
        object filter,
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var invokeMethod = filter.GetType().GetMethod(
            "InvokeAsync",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Filter type '{filter.GetType().FullName}' does not expose InvokeAsync.");

        var result = invokeMethod.Invoke(filter, [context, next]);
        return result switch
        {
            ValueTask<object?> valueTask => await valueTask.ConfigureAwait(false),
            Task<object?> task => await task.ConfigureAwait(false),
            ValueTask nonGenericValueTask => await AwaitNonGenericValueTask(nonGenericValueTask).ConfigureAwait(false),
            Task nonGenericTask => await AwaitNonGenericTask(nonGenericTask).ConfigureAwait(false),
            _ => result
        };
    }

    private static async Task<object?> AwaitNonGenericValueTask(ValueTask valueTask)
    {
        await valueTask.ConfigureAwait(false);
        return null;
    }

    private static async Task<object?> AwaitNonGenericTask(Task task)
    {
        await task.ConfigureAwait(false);
        return null;
    }

    private static async Task<int> RunEndpointCacheRegistryScenariosAsync(string tenantId, CancellationToken cancellationToken)
    {
        var checks = 0;
        var registry = new KyrolusEndpointCachePolicyRegistry();

        ExpectThrows<ArgumentNullException>(() => registry.SetDefault(null!));
        ExpectThrows<ArgumentException>(() => registry.SetForRoute("", "/api/menu", new KyrolusCachePolicy(Enabled: true)));
        ExpectThrows<ArgumentException>(() => registry.SetForRoute("GET", "", new KyrolusCachePolicy(Enabled: true)));
        ExpectThrows<ArgumentException>(() => registry.SetForTenant("", new KyrolusCachePolicy(Enabled: true)));
        ExpectThrows<ArgumentException>(() => registry.SetForTenantEndpoint("", EndpointNames.GetAll, new KyrolusCachePolicy(Enabled: true)));
        ExpectThrows<ArgumentException>(() => registry.SetForTenantEntity<RuntimeLinkItem>("", EndpointNames.GetAll, new KyrolusCachePolicy(Enabled: true)));
        ExpectThrows<ArgumentException>(() => registry.SetForTenantRoute("", "GET", "/api/menu", new KyrolusCachePolicy(Enabled: true)));
        checks++;

        var defaultPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "default");
        var endpointPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "endpoint");
        var entityPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "entity");
        var entityEndpointPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "entity-endpoint");
        var routePolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "route");
        var tenantPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant");
        var tenantEndpointPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant-endpoint");
        var tenantEntityPolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant-entity");
        var tenantRoutePolicy = new KyrolusCachePolicy(Enabled: true, KeySuffix: "tenant-route");

        registry
            .SetDefault(defaultPolicy)
            .SetForEndpoint(EndpointNames.GetAll, endpointPolicy)
            .SetForEntity<RuntimeLinkItem>(entityPolicy)
            .SetForEntity<RuntimeLinkItem>(EndpointNames.GetById, entityEndpointPolicy)
            .SetForRoute("GET", "/api/menu-items", routePolicy)
            .SetForTenant(tenantId, tenantPolicy)
            .SetForTenantEndpoint(tenantId, EndpointNames.GetById, tenantEndpointPolicy)
            .SetForTenantEntity<RuntimeLinkItem>(tenantId, EndpointNames.GetById, tenantEntityPolicy)
            .SetForTenantRoute(tenantId, "GET", "/api/menu-items", tenantRoutePolicy);

        var tenantEntityContext = new KyrolusEndpointCachePolicyContext(
            typeof(RuntimeLinkItem),
            nameof(RuntimeLinkItem),
            EndpointNames.GetById,
            "GET",
            "/api/menu-items",
            tenantId,
            "scope");
        var tenantEntityResolved = await registry.GetPolicyAsync(tenantEntityContext, cancellationToken).ConfigureAwait(false);
        if (tenantEntityResolved?.KeySuffix == "tenant-entity")
        {
            checks++;
        }

        var tenantRouteContext = tenantEntityContext with { Endpoint = EndpointNames.Query };
        var tenantRouteResolved = await registry.GetPolicyAsync(tenantRouteContext, cancellationToken).ConfigureAwait(false);
        if (tenantRouteResolved?.KeySuffix == "tenant-route")
        {
            checks++;
        }

        var routeContext = tenantRouteContext with { TenantId = null };
        var routeResolved = await registry.GetPolicyAsync(routeContext, cancellationToken).ConfigureAwait(false);
        if (routeResolved?.KeySuffix == "route")
        {
            checks++;
        }

        var fallbackContext = routeContext with { HttpMethod = "POST", Path = "/api/other", Endpoint = EndpointNames.Custom };
        var fallbackResolved = await registry.GetPolicyAsync(fallbackContext, cancellationToken).ConfigureAwait(false);
        if (fallbackResolved?.KeySuffix == "entity")
        {
            checks++;
        }

        var endpointFallbackContext = new KyrolusEndpointCachePolicyContext(
            typeof(RuntimeFieldSelectionOrder),
            nameof(RuntimeFieldSelectionOrder),
            EndpointNames.GetAll,
            "POST",
            "/api/none",
            null,
            null);
        var endpointFallbackResolved = await registry.GetPolicyAsync(endpointFallbackContext, cancellationToken).ConfigureAwait(false);
        if (endpointFallbackResolved?.KeySuffix == "endpoint")
        {
            checks++;
        }

        var defaultFallbackContext = new KyrolusEndpointCachePolicyContext(
            typeof(RuntimeFieldSelectionOrder),
            nameof(RuntimeFieldSelectionOrder),
            EndpointNames.Batch,
            "DELETE",
            "/api/none",
            null,
            null);
        var defaultFallbackResolved = await registry.GetPolicyAsync(defaultFallbackContext, cancellationToken).ConfigureAwait(false);
        if (defaultFallbackResolved?.KeySuffix == "default")
        {
            checks++;
        }

        await ExpectThrowsAsync<ArgumentNullException>(() => registry.GetPolicyAsync(null!, cancellationToken).AsTask()).ConfigureAwait(false);
        checks++;

        return checks;
    }

    private static async Task<int> RunIdempotencyStoreScenariosAsync(string tenantId, CancellationToken cancellationToken)
    {
        var checks = 0;
        var entry = new KyrolusIdempotencyEntry(new { Name = "idempotent" }, StatusCodes.Status200OK, "application/json");

        var inMemory = new KyrolusInMemoryIdempotencyStore();
        await inMemory.SetAsync("memory-key", entry, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var memoryHit = await inMemory.GetAsync("memory-key", cancellationToken).ConfigureAwait(false);
        if (memoryHit is not null)
        {
            checks++;
        }

        await inMemory.SetAsync("expired-key", entry, TimeSpan.FromMilliseconds(1), cancellationToken).ConfigureAwait(false);
        await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        var expired = await inMemory.GetAsync("expired-key", cancellationToken).ConfigureAwait(false);
        if (expired is null)
        {
            checks++;
        }

        var cacheProvider = new RuntimeInMemoryCacheProvider();
        var keyContext = new RuntimeCacheKeyContext("scope-x", "region-x", tenantId);
        var cacheStore = new KyrolusCacheIdempotencyStore(cacheProvider, keyContext);
        await cacheStore.SetAsync("cache-key", entry, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var cacheHit = await cacheStore.GetAsync("cache-key", cancellationToken).ConfigureAwait(false);
        if (cacheHit is not null)
        {
            checks++;
        }

        var tenantOnlyStore = new KyrolusCacheIdempotencyStore(
            cacheProvider,
            new RuntimeCacheKeyContext(null, null, tenantId));
        await tenantOnlyStore.SetAsync("tenant-key", entry, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var tenantHit = await tenantOnlyStore.GetAsync("tenant-key", cancellationToken).ConfigureAwait(false);
        if (tenantHit is not null)
        {
            checks++;
        }

        var fallbackStore = new KyrolusCacheIdempotencyStore(
            new NullCacheProvider(),
            new RuntimeCacheKeyContext("scope-fallback", "region-fallback", tenantId));
        await fallbackStore.SetAsync("fallback-key", entry, TimeSpan.FromMinutes(1), cancellationToken).ConfigureAwait(false);
        var fallbackHit = await fallbackStore.GetAsync("fallback-key", cancellationToken).ConfigureAwait(false);
        if (fallbackHit is not null)
        {
            checks++;
        }

        return checks;
    }

    private static IKyrolusMartenUnitOfWork<IDocumentSession> CreateUnitOfWorkWithoutSoftDelete<TEntity, TKey>(IDocumentSession session)
        where TEntity : class
        where TKey : IEquatable<TKey>
    {
        return new KyrolusMartenUnitOfWork<IDocumentSession>(
            session,
            serviceProvider: null,
            repositoryFactory: type =>
            {
                if (type == typeof(IKyrolusMartenRepositoryAsync<IDocumentSession, TEntity, TKey>))
                {
                    return new KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, TKey>(
                        session,
                        new KyrolusMartenRepositoryDependencies());
                }

                return null;
            });
    }

    private static string BuildSeekCursorToken(bool descending, IReadOnlyDictionary<string, string?> keys)
    {
        var payload = new RuntimeSeekTokenPayload(descending, keys);
        var json = JsonSerializer.Serialize(payload, RuntimeSeekTokenSerializerOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record RuntimeSeekTokenPayload(bool Descending, IReadOnlyDictionary<string, string?> Keys);

    private static readonly JsonSerializerOptions RuntimeSeekTokenSerializerOptions = new(JsonSerializerDefaults.Web);

    private static void ExpectThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception '{typeof(TException).Name}' was not thrown.");
    }

    private static async Task ExpectThrowsAsync<TException>(Func<Task> action)
        where TException : Exception
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException($"Expected exception '{typeof(TException).Name}' was not thrown.");
    }

    private static KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid> CreateRepositoryWithCacheEnabled<TEntity>(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId)
        where TEntity : class
    {
        var dependencies = new KyrolusMartenRepositoryDependencies(
            CacheProvider: cacheProvider,
            CacheKeyContext: new RuntimeCacheKeyContext($"diag-scope:{tenantId}", "diag-region", tenantId),
            CachePolicyProvider: new RuntimeRepositoryCachePolicyProvider(),
            CachePolicy: new KyrolusCachePolicy(
                AbsoluteExpirationRelativeToNow: TimeSpan.FromMinutes(2),
                SlidingExpiration: TimeSpan.FromSeconds(30),
                Jitter: TimeSpan.FromSeconds(1),
                NegativeCacheTtl: TimeSpan.FromSeconds(5),
                Enabled: true,
                KeySuffix: "base",
                ExtraInvalidationKeys: ["diag:{entity}:{tenant}:{id}", "{all}:extra"],
                ExtraInvalidationKeyPatterns: ["diag-pattern:{entity}:{tenant}:{scope}:{id}"]),
            PolicyProvider: new RuntimeRepositoryPolicyProvider(cacheProvider, tenantId));

        return new KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>(session, dependencies);
    }

    private static KyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid> CreateSoftDeleteRepository(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId,
        IKyrolusMartenSoftDeletePolicy softDeletePolicy)
    {
        var dependencies = new KyrolusMartenRepositoryDependencies(
            SoftDeletePolicy: softDeletePolicy,
            CacheProvider: cacheProvider,
            CacheKeyContext: new RuntimeCacheKeyContext($"soft-scope:{tenantId}", "soft-region", tenantId),
            CachePolicyProvider: new RuntimeRepositoryCachePolicyProvider(),
            CachePolicy: new KyrolusCachePolicy(
                Enabled: true,
                KeySuffix: "soft",
                ExtraInvalidationKeys: ["soft:{entity}:{tenant}:{id}"],
                ExtraInvalidationKeyPatterns: ["soft-pattern:{entity}:{scope}:{id}"]));

        return new KyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>(session, dependencies);
    }

    private static KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid> CreateRepositoryWithCacheDisabled<TEntity>(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId)
        where TEntity : class
    {
        var dependencies = new KyrolusMartenRepositoryDependencies(
            CacheProvider: cacheProvider,
            CacheKeyContext: new RuntimeCacheKeyContext(null, null, tenantId),
            CachePolicy: new KyrolusCachePolicy(
                Enabled: false,
                ExtraInvalidationKeys: ["disabled:{entity}:{id}:{tenant}"],
                ExtraInvalidationKeyPatterns: ["disabled-pattern:{entity}:{scope}"]));

        return new KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>(session, dependencies);
    }

    private static KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid> CreateRepositoryWithoutCacheProvider<TEntity>(
        IDocumentSession session,
        string tenantId)
        where TEntity : class
    {
        var dependencies = new KyrolusMartenRepositoryDependencies(
            CacheKeyContext: new RuntimeCacheKeyContext(null, null, tenantId),
            CachePolicy: new KyrolusCachePolicy(Enabled: true));

        return new KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>(session, dependencies);
    }
}
