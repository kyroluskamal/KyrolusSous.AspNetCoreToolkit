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

internal sealed class DisposableScope(Action onDispose) : IDisposable
{
    private readonly Action onDispose = onDispose;

    public void Dispose() => onDispose();
}

internal sealed class FixedRandom(double value) : Random
{
    private readonly double value = value;

    public override double NextDouble() => value;
}

internal sealed record DiagnosticsAuthorizationContext(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions) : IKyrolusMartenAuthorizationContext
{
    public string? UserId { get; init; } = "diag-user";
    public string? TenantId { get; init; } = "diag-tenant";
}

internal sealed class DiagnosticsValidatablePayload : IKyrolusMartenValidatable
{
    public void Validate()
    {
    }
}

internal sealed class DiagnosticsAsyncValidatablePayload : IKyrolusMartenAsyncValidatable
{
    public Task ValidateAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public sealed class MenuItemCountCompiledQuery : ICompiledQuery<MenuItem, int>
{
    public string Category { get; set; } = string.Empty;
    public decimal MinPrice { get; set; }
    public string[] Tags { get; set; } = [];

    public Expression<Func<IMartenQueryable<MenuItem>, int>> QueryIs()
        => query => query.Count(x => x.Category == Category && x.Price >= MinPrice);
}

internal sealed class RuntimeRepositoryPolicyProvider(ICacheProvider cacheProvider, string tenantId) : IKyrolusMartenRepositoryPolicyProvider
{
    public ValueTask<KyrolusMartenRepositoryDependencies?> GetPolicyAsync(
        KyrolusMartenRepositoryPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        var dependencies = new KyrolusMartenRepositoryDependencies(
            Observer: new RuntimeObserver(),
            Authorization: new RuntimeAuthorization(),
            Validation: new RuntimeValidation(),
            SoftDeletePolicy: KyrolusMartenSoftDeletePolicy.IsDeleted(),
            CacheProvider: cacheProvider,
            CacheKeyContext: new RuntimeCacheKeyContext($"policy-scope:{context.EntityName}", "policy-region", tenantId),
            CachePolicyProvider: new RuntimeRepositoryCachePolicyProvider(),
            CachePolicy: new KyrolusCachePolicy(
                Enabled: true,
                KeySuffix: "policy",
                ExtraInvalidationKeys: ["policy:{entity}:{tenant}:{id}", "{all}:policy"],
                ExtraInvalidationKeyPatterns: ["policy-pattern:{entity}:{scope}:{id}"]),
            ResiliencePolicy: new RuntimeResiliencePolicy(),
            Tracing: new RuntimeTracing());

        return ValueTask.FromResult<KyrolusMartenRepositoryDependencies?>(dependencies);
    }
}

internal sealed class RuntimeRepositoryCachePolicyProvider : IKyrolusRepositoryCachePolicyProvider
{
    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default)
    {
        var policy = new KyrolusCachePolicy(
            Enabled: true,
            KeySuffix: "dynamic",
            ExtraInvalidationKeys: ["dynamic:{entity}:{tenant}:{id}"],
            ExtraInvalidationKeyPatterns: ["dynamic-pattern:{entity}:{scope}:{id}"]);

        return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
    }
}

internal sealed class RuntimeCacheKeyContext(string? scopeKey, string? region, string? tenantId) : ICacheKeyContext
{
    public string? ScopeKey { get; } = scopeKey;
    public string? Region { get; } = region;
    public string? TenantId { get; } = tenantId;
}

internal sealed class RuntimeObserver : IKyrolusMartenObserver
{
    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class RuntimeAuthorization : IKyrolusMartenAuthorization
{
    public Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}

internal sealed class RuntimeValidation : IKyrolusMartenValidation
{
    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

internal sealed class RuntimeResiliencePolicy : IKyrolusMartenResiliencePolicy
{
    public Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => action();

    public Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
        => action();
}

internal sealed class RuntimeTracing : IKyrolusMartenTracing
{
    public IDisposable? StartScope(string operation, object? payload = null) => null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class RuntimeInMemoryCacheProvider : ICacheProvider
{
    private readonly ConcurrentDictionary<string, object?> store = new(StringComparer.Ordinal);
    public ConcurrentQueue<string> RemovedKeys { get; } = new();
    public ConcurrentQueue<string> RemovedPatterns { get; } = new();

    public Task<T?> GetAsync<T>(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(cacheKey, out var value))
        {
            return Task.FromResult(default(T?));
        }

        if (value is null)
        {
            return Task.FromResult(default(T?));
        }

        return Task.FromResult((T?)value);
    }

    public Task SetAsync<T>(string cacheKey, T value, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task SetAsync<T>(string cacheKey, T value, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        store[cacheKey] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        RemovedKeys.Enqueue(cacheKey);
        store.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string cacheKey, CancellationToken cancellationToken = default)
        => Task.FromResult(store.ContainsKey(cacheKey));

    public Task RemoveKeysByPatternAsync(string keyPattern, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyPattern))
        {
            return Task.CompletedTask;
        }

        RemovedPatterns.Enqueue(keyPattern);
        var regex = BuildRegex(keyPattern);
        foreach (var key in store.Keys.Where(key => regex.IsMatch(key)))
        {
            store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task<IDictionary<string, T?>> GetManyAsync<T>(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, T?>(StringComparer.Ordinal);
        foreach (var key in cacheKeys)
        {
            if (store.TryGetValue(key, out var value) && value is not null)
            {
                results[key] = (T?)value;
            }
            else
            {
                results[key] = default;
            }
        }

        return Task.FromResult<IDictionary<string, T?>>(results);
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, TimeSpan expirationTime = default, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            store[item.Key] = item.Value;
        }

        return Task.CompletedTask;
    }

    public Task SetManyAsync<T>(IReadOnlyCollection<KeyValuePair<string, T>> items, KyrolusCacheEntryOptions? options, CancellationToken cancellationToken = default)
    {
        foreach (var item in items)
        {
            store[item.Key] = item.Value;
        }

        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(IReadOnlyCollection<string> cacheKeys, CancellationToken cancellationToken = default)
    {
        foreach (var key in cacheKeys)
        {
            store.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public async Task<T> GetOrCreateAsync<T>(
        string cacheKey,
        Func<CancellationToken, Task<T>> factory,
        KyrolusCacheEntryOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        if (store.TryGetValue(cacheKey, out var existing))
        {
            if (existing is null)
            {
                return default!;
            }

            return (T)existing;
        }

        var value = await factory(cancellationToken).ConfigureAwait(false);
        store[cacheKey] = value;
        return value;
    }

    private static Regex BuildRegex(string pattern)
    {
        var escaped = Regex.Escape(pattern);
        var regexPattern = "^" + escaped.Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(regexPattern, RegexOptions.CultureInvariant);
    }
}

internal sealed class RuntimeVersionProbeMetadata
{
    public Guid? Version { get; init; }
    public string? ETag { get; init; }
}

internal sealed class RuntimeStringIdDocument
{
    public string Id { get; set; } = string.Empty;
}

internal sealed class RuntimeGuidIdDocument
{
    public Guid Id { get; set; }
}

internal sealed class RuntimeNoIdDocument
{
    public string Name { get; set; } = string.Empty;
}

internal sealed class RuntimeRepositoryUtilityProbe<TEntity>(
    IDocumentSession session,
    KyrolusMartenRepositoryDependencies dependencies)
    : KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>(session, dependencies)
    where TEntity : class
{
    public Task ProbeEnsurePolicyInitializedAsync(CancellationToken cancellationToken)
        => EnsurePolicyInitializedAsync(cancellationToken);

    public ValueTask<KyrolusCachePolicy> ProbeResolveCachePolicyAsync(
        string operation,
        string? tenantId,
        CancellationToken cancellationToken)
        => ResolveCachePolicyAsync(operation, tenantId, cancellationToken);

    public KyrolusCacheEntryOptions ProbeBuildCacheEntryOptions(KyrolusCachePolicy policy, string? tenantId)
        => BuildCacheEntryOptions(policy, tenantId);

    public string ProbeBuildCacheKey(string? tenantId, Guid id, string? policySuffix = null)
        => BuildCacheKey(tenantId, id, policySuffix);

    public string ProbeBuildCacheAllKey(string? tenantId, string? policySuffix = null)
        => BuildCacheAllKey(tenantId, policySuffix);

    public IDocumentSession ProbeResolveSession(string? tenantId)
        => ResolveSession(tenantId);

    public Task<TEntity?> ProbePatchEntityAsync(
        Guid id,
        Dictionary<string, object> updates,
        IDocumentSession session,
        CancellationToken cancellationToken)
        => PatchEntityAsync(id, updates, session, cancellationToken);

    public Task<object?> ProbeLoadAsync(
        Type docType,
        object id,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        return (Task<object?>)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(this, [docType, id, session, cancellationToken])!;
    }

    public Task<IReadOnlyList<object>> ProbeLoadManyAsync(
        Type docType,
        System.Collections.IEnumerable ids,
        IDocumentSession session,
        CancellationToken cancellationToken)
    {
        return (Task<IReadOnlyList<object>>)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("LoadManyAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(this, [docType, ids, session, cancellationToken])!;
    }

    public static Guid? ProbeReadVersion(object? metadata)
        => ReadVersion(metadata);

    public static void ProbeApplyProperty(TEntity entity, string propertyName, object? rawValue)
        => ApplyProperty(entity, propertyName, rawValue);

    public static object? ProbeNormalizeValue(object? rawValue, Type targetType)
        => NormalizeValue(rawValue, targetType);

    public static List<string> ProbeMergeIncludes(
        List<string>? includeProperties,
        Expression<Func<TEntity, object?>>[]? includeExpressions)
    {
        return (List<string>)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("MergeIncludes", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [includeProperties, includeExpressions])!;
    }

    public static bool ProbeTryGetCollectionElementType(Type type, out Type elementType)
    {
        var args = new object?[] { type, null };
        var result = (bool)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("TryGetCollectionElementType", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        elementType = (Type)args[1]!;
        return result;
    }

    public static PropertyInfo? ProbeResolveIdProperty(Type entityType, string includeName)
    {
        return (PropertyInfo?)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("ResolveIdProperty", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entityType, includeName]);
    }

    public static PropertyInfo? ProbeResolveIdsProperty(Type entityType, string includeName)
    {
        return (PropertyInfo?)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("ResolveIdsProperty", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entityType, includeName]);
    }

    public static object? ProbeConvertId(object? value, Type targetType)
    {
        return typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("ConvertId", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [value, targetType]);
    }

    public static object ProbeCreateTypedIdCollection(System.Collections.IEnumerable ids, Type parameterType)
    {
        return typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("CreateTypedIdCollection", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [ids, parameterType])!;
    }

    public static Type ProbeResolveDocumentIdType(Type docType, Type fallbackType)
    {
        return (Type)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("ResolveDocumentIdType", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [docType, fallbackType])!;
    }

    public static void ProbeSetCollectionValue(TEntity entity, string propertyName, Type elementType, IReadOnlyList<object> items)
    {
        var prop = typeof(TEntity).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"Property '{propertyName}' was not found on {typeof(TEntity).Name}.");
        typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("SetCollectionValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [entity, prop, elementType, items]);
    }

    public static string? ProbeTryResolveSessionTenantId(IDocumentSession session)
    {
        return (string?)typeof(KyrolusMartenRepositoryAsync<IDocumentSession, TEntity, Guid>)
            .GetMethod("TryResolveSessionTenantId", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [session]);
    }
}

internal static class RuntimeGetSeekHandlerProbe<TResponse>
    where TResponse : class
{
    private static readonly Type HandlerType = typeof(GetSeekQueryHandler<IDocumentSession, TResponse, Guid>);

    public static string? ProbeBuildNextToken(IReadOnlyList<TResponse> items, IReadOnlyList<string> properties, bool descending)
    {
        return (string?)HandlerType
            .GetMethod("BuildNextToken", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [items, properties, descending]);
    }

    public static bool ProbeTryBuildSeekPredicate(
        IReadOnlyList<string> properties,
        IReadOnlyDictionary<string, string?> values,
        bool descending,
        out Expression<Func<TResponse, bool>>? predicate,
        out string? error)
    {
        var args = new object?[] { properties, values, descending, null, null };
        var result = (bool)HandlerType
            .GetMethod("TryBuildSeekPredicate", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        predicate = (Expression<Func<TResponse, bool>>?)args[3];
        error = (string?)args[4];
        return result;
    }

    public static bool ProbeTryBuildMemberAccess(string propertyPath, out Type? memberType, out string? error)
    {
        var args = new object?[]
        {
            Expression.Parameter(typeof(TResponse), "e"),
            propertyPath,
            null,
            null,
            null
        };

        var result = (bool)HandlerType
            .GetMethod("TryBuildMemberAccess", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        memberType = args[3] as Type;
        error = (string?)args[4];
        return result;
    }

    public static bool ProbeTryGetPropertyValue(TResponse entity, string propertyPath, out object? value)
    {
        var args = new object?[] { entity, propertyPath, null };
        var result = (bool)HandlerType
            .GetMethod("TryGetPropertyValue", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        value = args[2];
        return result;
    }

    public static bool ProbeTryConvert(string? raw, Type targetType, out object? result)
    {
        var args = new object?[] { raw, targetType, null };
        var success = (bool)HandlerType
            .GetMethod("TryConvert", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, args)!;
        result = args[2];
        return success;
    }
}

internal sealed class RuntimeValidationProbeRequest : IKyrolusValidationCacheable, IKyrolusValidationNegativeCacheable
{
    public decimal Price { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CacheKey { get; init; }
    public TimeSpan? CacheTtl { get; init; }
    public KyrolusValidationCacheMode CacheMode { get; init; } = KyrolusValidationCacheMode.All;
    public TimeSpan? NegativeCacheTtl { get; init; }
}

internal sealed class RuntimeNoValidatorValidationProbeRequest : IKyrolusValidationCacheable, IKyrolusValidationNegativeCacheable
{
    public string? CacheKey { get; init; }
    public TimeSpan? CacheTtl { get; init; }
    public KyrolusValidationCacheMode CacheMode { get; init; } = KyrolusValidationCacheMode.SuccessOnly;
    public TimeSpan? NegativeCacheTtl { get; init; }
}

internal sealed class RuntimeNoValidatorFluentProbeRequest;

internal sealed class RuntimeFluentValidationProbeRequest
{
    public string Name { get; init; } = string.Empty;
    public int CreatedBy { get; init; }
    public int Id { get; init; }
    public string Description { get; init; } = string.Empty;
    public string Color { get; init; } = string.Empty;
    public string[] Tags { get; init; } = Array.Empty<string>();
    public string? Url { get; init; }
    public string? OptionalUrl { get; init; }
    public string? StrictUrl { get; init; }
}

internal sealed class RuntimeFluentValidationProbeValidator : AbstractValidator<RuntimeFluentValidationProbeRequest>
{
    public RuntimeFluentValidationProbeValidator()
    {
        RuleFor(x => x.Name)
            .Required(x => x.Name)
            .WithErrorCode("name.required")
            .WithSeverity(Severity.Warning)
            .WithState(_ => new KyrolusValidationGroup("api"));

        RuleFor(x => x.CreatedBy)
            .ShouldCreatedBySomeone(x => x.CreatedBy)
            .WithErrorCode("createdby.invalid")
            .WithState(_ => "audit");

        RuleFor(x => x.Id)
            .IdCanNotBeZero(x => x.Id)
            .WithErrorCode("id.invalid")
            .WithState(_ => new Dictionary<string, object?> { ["group"] = "identity" });

        RuleFor(x => x.Description)
            .HasMaximumLength(5, x => x.Description)
            .WithErrorCode("description.max");

        RuleFor(x => x.Color)
            .IsColor(x => x.Color)
            .WithErrorCode("color.invalid");

        RuleFor(x => x.Tags)
            .ArrayNotEmpty(x => x.Tags)
            .WithErrorCode("tags.empty");

        RuleFor(x => x.Url!)
            .IsUrl(x => x.Url!, propertyName: "payload.url")
            .WithErrorCode("url.invalid");

        RuleFor(x => x.OptionalUrl!)
            .IsUrl(x => x.OptionalUrl!, isNullOrEmpty: true)
            .WithErrorCode("optional.url");

        RuleSet("strict", () =>
        {
            RuleFor(x => x.StrictUrl!)
                .IsUrl(x => x.StrictUrl!)
                .WithErrorCode("strict.url")
                .WithSeverity(Severity.Info)
                .WithState(_ => "strict-group");
        });
    }
}

internal sealed class RuntimeScannedValidationRequest;

internal sealed class RuntimeValidationProbeRequestValidator : IKyrolusRequestValidator<RuntimeValidationProbeRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        RuntimeValidationProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        var failures = new List<KyrolusValidationFailure>();

        if (request.Price <= 0)
        {
            failures.Add(new KyrolusValidationFailure(
                PropertyName: "Price",
                ErrorMessage: "price.invalid",
                ErrorCode: "price",
                Severity: KyrolusValidationSeverity.Error,
                MessageKey: "price.invalid"));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            failures.Add(new KyrolusValidationFailure(
                PropertyName: "Name",
                ErrorMessage: "name.required",
                ErrorCode: "name.required",
                Severity: KyrolusValidationSeverity.Warning,
                RuleSet: "strict",
                Group: "api",
                MessageKey: "name.required"));
        }

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(failures);
    }
}

internal sealed class RuntimeValidationProbeContextValidator : IKyrolusRequestValidatorWithContext<RuntimeValidationProbeRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        RuntimeValidationProbeRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValidateAsync(request, KyrolusValidationContext.Default, cancellationToken);
    }

    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        RuntimeValidationProbeRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (context.Groups is not { Count: > 0 } || !context.Groups.Contains("api", StringComparer.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(Array.Empty<KyrolusValidationFailure>());
        }

        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(
        [
            new KyrolusValidationFailure(
                PropertyName: "Context",
                ErrorMessage: "context.filtered",
                ErrorCode: "context.filtered",
                Severity: KyrolusValidationSeverity.Info,
                RuleSet: "strict",
                Group: "api",
                MessageKey: "context.filtered")
        ]);
    }
}

internal sealed class RuntimeScannedValidationRequestValidator : IKyrolusRequestValidator<RuntimeScannedValidationRequest>
{
    public ValueTask<IReadOnlyList<KyrolusValidationFailure>> ValidateAsync(
        RuntimeScannedValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<KyrolusValidationFailure>>(
        [
            new KyrolusValidationFailure(
                PropertyName: "Scanned",
                ErrorMessage: "scanned.invalid",
                ErrorCode: "scanned.invalid",
                Severity: KyrolusValidationSeverity.Error,
                RuleSet: "default",
                Group: "default",
                MessageKey: "scanned.invalid")
        ]);
    }
}

internal sealed class RuntimeValidationHook : IKyrolusValidationHook
{
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public ValueTask OnBeforeAsync(
        object? request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        BeforeCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask OnAfterAsync(
        object? request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default)
    {
        AfterCount++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class RuntimeTypedValidationHook : IKyrolusValidationHook<RuntimeValidationProbeRequest>
{
    public int BeforeCount { get; private set; }
    public int AfterCount { get; private set; }

    public ValueTask OnBeforeAsync(
        RuntimeValidationProbeRequest request,
        KyrolusValidationContext context,
        CancellationToken cancellationToken = default)
    {
        BeforeCount++;
        return ValueTask.CompletedTask;
    }

    public ValueTask OnAfterAsync(
        RuntimeValidationProbeRequest request,
        KyrolusValidationContext context,
        IReadOnlyList<KyrolusValidationFailure> failures,
        CancellationToken cancellationToken = default)
    {
        AfterCount++;
        return ValueTask.CompletedTask;
    }
}

internal sealed class RuntimeStringLocalizer(IReadOnlyDictionary<string, string> map) : IStringLocalizer
{
    private readonly IReadOnlyDictionary<string, string> map = map;

    public LocalizedString this[string name]
        => map.TryGetValue(name, out var value)
            ? new LocalizedString(name, value, resourceNotFound: false)
            : new LocalizedString(name, name, resourceNotFound: true);

    public LocalizedString this[string name, params object[] arguments]
    {
        get
        {
            var resolved = this[name];
            var value = resolved.ResourceNotFound
                ? name
                : string.Format(CultureInfo.CurrentCulture, resolved.Value, arguments);
            return new LocalizedString(name, value, resolved.ResourceNotFound);
        }
    }

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
    {
        foreach (var (key, value) in map)
        {
            yield return new LocalizedString(key, value, resourceNotFound: false);
        }
    }

    public IStringLocalizer WithCulture(CultureInfo culture) => this;
}

internal sealed class RuntimeTypedStringLocalizer<T>(IReadOnlyDictionary<string, string> map) : IStringLocalizer<T>
{
    private readonly RuntimeStringLocalizer inner = new(map);

    public LocalizedString this[string name] => inner[name];

    public LocalizedString this[string name, params object[] arguments] => inner[name, arguments];

    public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => inner.GetAllStrings(includeParentCultures);

    public IStringLocalizer WithCulture(CultureInfo culture) => this;
}

internal sealed class RuntimeExceptionResource;

internal sealed class RuntimeCachePayload
{
    public string Name { get; init; } = string.Empty;
    public int Count { get; init; }
}

internal sealed class RuntimeMissingCachePayload
{
    public string Value { get; init; } = string.Empty;
}

[JsonSerializable(typeof(RuntimeCachePayload))]
internal sealed partial class RuntimeCacheJsonContext : JsonSerializerContext;

internal sealed class RuntimeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;
    public string ApplicationName { get; set; } = "Kyrolus.Diagnostics";
    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed record RuntimeSagaState(string Status, int Step);

internal sealed record RuntimeEvent(string Action, DateTime OccurredAtUtc);

internal sealed class RuntimeNullStringStreamId
{
    public override string? ToString() => null;
}

internal sealed record RuntimeProjectionEvent(string Name);

internal enum RuntimeSeekProbeStatus
{
    New = 0,
    Active = 1
}

internal sealed class RuntimeQueryBuilderProbe
{
    public Guid Id { get; set; }
    public Guid DirectId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTime HappenedOn { get; set; }
    public TimeSpan Duration { get; set; }
    public RuntimeSeekProbeStatus StatusFromText { get; set; }
    public RuntimeSeekProbeStatus StatusFromNumber { get; set; }
    public int Sequence { get; set; }
    public int? OptionalSequence { get; set; }
    public RuntimeQueryBuilderNested Nested { get; set; } = new(string.Empty);
}

internal sealed class RuntimeQueryBuilderNested(string name)
{
    public string Name { get; set; } = name;
}

internal sealed class RuntimeSeekProbe
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public long Rank { get; set; }
    public decimal Amount { get; set; }
    public DateTime HappenedOn { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public RuntimeSeekProbeStatus Status { get; set; }
}

internal sealed class RuntimeGetSeekNestedEnvelope
{
    public RuntimeSeekProbe? Probe { get; set; }
}

internal sealed class RuntimeFieldSelectionOrder
{
    public Guid Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public RuntimeFieldSelectionCategory Category { get; set; } = new();
    public List<RuntimeFieldSelectionLine> Lines { get; set; } = [];
    public RuntimeFieldSelectionLine[] LineArray { get; set; } = [];
    public IReadOnlyCollection<RuntimeFieldSelectionLine> ReadOnlyLines { get; set; } = [];
    public RuntimeFieldSelectionLineBag CustomEnumerableLines { get; set; } = new([]);
}

internal sealed class RuntimeFieldSelectionCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class RuntimeFieldSelectionLine
{
    public string Product { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

internal sealed class RuntimeFieldSelectionLineBag(IEnumerable<RuntimeFieldSelectionLine> items) : IEnumerable<RuntimeFieldSelectionLine>
{
    private readonly IReadOnlyList<RuntimeFieldSelectionLine> items = items.ToList();

    public IEnumerator<RuntimeFieldSelectionLine> GetEnumerator() => items.GetEnumerator();

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class RuntimeLinkItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class RuntimeOpenApiProjection
{
    public Guid Id { get; set; }
}

[KyrolusOutputCache(Enabled = true, KeySuffix = "attribute")]
internal sealed class RuntimeOutputCacheAttributedModel
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

internal sealed class RuntimeEndpointFilterInvocationContext(HttpContext httpContext, params object?[] arguments)
    : EndpointFilterInvocationContext
{
    private readonly object?[] arguments = arguments;

    public override HttpContext HttpContext { get; } = httpContext;

    public override IList<object?> Arguments => arguments;

    public override T GetArgument<T>(int index)
    {
        return (T)arguments[index]!;
    }
}

internal sealed class RuntimeNoopLinkGenerator : LinkGenerator
{
    public override string? GetPathByAddress<TAddress>(
        TAddress address,
        RouteValueDictionary values,
        PathString pathBase = default,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return null;
    }

    public override string? GetUriByAddress<TAddress>(
        TAddress address,
        RouteValueDictionary values,
        string scheme,
        HostString host,
        PathString pathBase = default,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return null;
    }

    public override string? GetPathByAddress<TAddress>(
        HttpContext httpContext,
        TAddress address,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues = null,
        PathString? pathBase = null,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return null;
    }

    public override string? GetUriByAddress<TAddress>(
        HttpContext httpContext,
        TAddress address,
        RouteValueDictionary values,
        RouteValueDictionary? ambientValues = null,
        string? scheme = null,
        HostString? host = null,
        PathString? pathBase = null,
        FragmentString fragment = default,
        LinkOptions? options = null)
    {
        return null;
    }
}

internal sealed class RuntimeMappedCqrsExceptionMapper<TResponse, TException>(TResponse response)
    : IKyrolusExceptionMapper<TResponse>
    where TException : Exception
{
    public bool TryMap(Exception exception, out TResponse mappedResponse)
    {
        if (exception is TException)
        {
            mappedResponse = response;
            return true;
        }

        mappedResponse = default!;
        return false;
    }
}

internal sealed class CountingProjectionOrchestrator : IKyrolusMartenProjectionOrchestrator
{
    public int RebuildCalls { get; private set; }
    public int UpToDateCalls { get; private set; }

    public Task EnqueueRebuildAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        RebuildCalls++;
        return Task.CompletedTask;
    }

    public Task ApplyEventAsync(object @event, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task EnsureUpToDateAsync(string projectionName, CancellationToken cancellationToken = default)
    {
        UpToDateCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeProjectionWrapper(object? value)
{
    public object? Value { get; } = value;
}

internal sealed class RuntimeProjectionDescriptor(string projectionName)
{
    public string ProjectionName { get; } = projectionName;
}

internal sealed class RuntimeNameOnlyProjection(string name)
{
    public string Name { get; } = name;
}

internal sealed class RuntimeUnnamedProjection;

internal sealed class RuntimeStringShardMethodHolder
{
    public void StartStringShard(string shardName)
    {
    }
}

internal sealed class RuntimeShardName(string name)
{
    public string Name { get; } = name;
}

internal sealed class RuntimeInvocationProbe
{
    public int SyncCalls { get; private set; }
    public int AsyncCalls { get; private set; }

    public void RunSync()
    {
        SyncCalls++;
    }

    public Task RunAsync()
    {
        AsyncCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeDaemonLifecycleProbe
{
    public int StartAllCalls { get; private set; }
    public List<string> StartedShards { get; } = [];

    public Task StartAllShards()
    {
        StartAllCalls++;
        return Task.CompletedTask;
    }

    public Task StartShard(RuntimeShardName shardName, CancellationToken cancellationToken)
    {
        StartedShards.Add(shardName.Name);
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeDaemonLifecycleWithTokenProbe
{
    public int StartAllCalls { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public Task StartAllShards(CancellationToken cancellationToken)
    {
        StartAllCalls++;
        LastToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeStringShardDaemonLifecycleProbe
{
    public List<string> StartedShards { get; } = [];

    public Task StartShard(string shardName)
    {
        StartedShards.Add(shardName);
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeWaitForNonStaleTokenDaemon
{
    public int WaitCalls { get; private set; }
    public CancellationToken LastToken { get; private set; }

    public Task WaitForNonStaleData(CancellationToken cancellationToken)
    {
        WaitCalls++;
        LastToken = cancellationToken;
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeParameterlessWaitDaemon
{
    public int WaitCalls { get; private set; }

    public Task WaitForNonStaleData()
    {
        WaitCalls++;
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeNoWaitProjectionDaemon;

internal sealed class RuntimeSingleArgRebuildDaemon
{
    public List<string> RebuiltProjectionNames { get; } = [];

    public Task RebuildProjection(string projectionName)
    {
        RebuiltProjectionNames.Add(projectionName);
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeTwoArgRebuildDaemon
{
    public List<string> RebuiltProjectionNames { get; } = [];

    public Task RebuildProjection(string projectionName, CancellationToken cancellationToken)
    {
        RebuiltProjectionNames.Add(projectionName);
        return Task.CompletedTask;
    }
}

internal sealed class RuntimeNoRebuildProjectionDaemon;

internal sealed class RuntimeCustomRepository;

internal sealed class RuntimeFactoryRepository;

internal sealed class RuntimeMissingRepository;

internal sealed class StaticTenantResolver(string tenantId) : ITenantResolver
{
    public string? ResolveTenantId() => tenantId;
}
