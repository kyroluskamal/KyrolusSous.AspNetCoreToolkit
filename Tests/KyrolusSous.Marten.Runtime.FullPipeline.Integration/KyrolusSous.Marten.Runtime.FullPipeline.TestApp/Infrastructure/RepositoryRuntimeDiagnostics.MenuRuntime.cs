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
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunMenuRuntimeAsync(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var repository = CreateRepositoryWithCacheEnabled<MenuItem>(session, cacheProvider, tenantId);
        return await RunMenuRuntimeCoreAsync(
            repository,
            session,
            tenantId,
            "menu-runtime",
            includeJsonObjectPatch: true,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunMenuRuntimeCacheDisabledAsync(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var repository = CreateRepositoryWithCacheDisabled<MenuItem>(session, cacheProvider, tenantId);
        return await RunMenuRuntimeCoreAsync(
            repository,
            session,
            tenantId,
            "menu-runtime-cache-disabled",
            includeJsonObjectPatch: false,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunMenuRuntimeNoCacheProviderAsync(
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var repository = CreateRepositoryWithoutCacheProvider<MenuItem>(session, tenantId);
        return await RunMenuRuntimeCoreAsync(
            repository,
            session,
            tenantId,
            "menu-runtime-no-cache-provider",
            includeJsonObjectPatch: false,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunMenuSoftDeleteRuntimeAsync(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var repository = CreateSoftDeleteRepository(
            session,
            cacheProvider,
            tenantId,
            KyrolusMartenSoftDeletePolicy.IsDeleted());
        repository.SetObserver(new RuntimeObserver());

        var activeOne = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Soft-A-{Guid.NewGuid():N}",
            Category = "DiagSoft",
            Price = 11,
            IsDeleted = false
        };

        var activeTwo = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Soft-B-{Guid.NewGuid():N}",
            Category = "DiagSoft",
            Price = 22,
            IsDeleted = false
        };

        var deletedSeed = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Soft-Deleted-{Guid.NewGuid():N}",
            Category = "DiagSoft",
            Price = 33,
            IsDeleted = true
        };

        await repository.AddRangeAsync([activeOne, activeTwo, deletedSeed], cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var crossTenantCount = (await repository.GetAllAsync(
            new MartenQueryOptions<MenuItem>(TenantId: $"{tenantId}-other", IncludeSoftDeleted: true),
            cancellationToken).ConfigureAwait(false)).Count();

        var activeBefore = (await repository.GetAllAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false)).ToList();
        var includeDeletedBefore = (await repository.GetAllIncludingDeletedAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false)).ToList();
        var deletedOnlyBefore = (await repository.GetDeletedOnlyAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false)).ToList();

        var byIdActive = await repository.GetByIdAsync(activeOne.Id, new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);
        var byIdDeletedFiltered = await repository.GetByIdAsync(deletedSeed.Id, new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);
        var byIdDeletedIncluding = await repository.GetByIdIncludingDeletedAsync(deletedSeed.Id, new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);

        var byIdFallback = await repository.GetByIdAsync(
            activeOne.Id,
            new MartenQueryOptions<MenuItem>(IncludeProperties: ["UnknownInclude"]),
            cancellationToken).ConfigureAwait(false);
        var byIdIncludingFallback = await repository.GetByIdIncludingDeletedAsync(
            deletedSeed.Id,
            new MartenQueryOptions<MenuItem>(IncludeProperties: ["UnknownInclude"]),
            cancellationToken).ConfigureAwait(false);

        _ = await repository.GetAllAsync(new MartenQueryOptions<MenuItem>(IncludeSoftDeleted: true), cancellationToken).ConfigureAwait(false);
        _ = await repository.GetAllIncludingDeletedAsync(
            new MartenQueryOptions<MenuItem>(Filter: x => x.Category == "DiagSoft"),
            cancellationToken).ConfigureAwait(false);
        _ = await repository.GetDeletedOnlyAsync(
            new MartenQueryOptions<MenuItem>(Filter: x => x.Category == "DiagSoft"),
            cancellationToken).ConfigureAwait(false);

        var streamCount = 0;
        await foreach (var _ in repository.StreamAsync(new MartenQueryOptions<MenuItem>(), cancellationToken))
        {
            streamCount++;
        }

        var page = await repository.GetPageAsync(
            new MartenQueryOptions<MenuItem>(),
            new MartenPageRequest(PageNumber: 1, PageSize: 10),
            cancellationToken).ConfigureAwait(false);

        var removedEntity = await repository.RemoveAsync(activeOne, null, null, cancellationToken).ConfigureAwait(false);
        var removedById = await repository.RemoveAsync(activeTwo.Id, null, null, cancellationToken).ConfigureAwait(false);
        var deleteWhereResult = await repository.DeleteWhereAsync(x => x.Category == "DiagSoft", null, cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var restoreById = await repository.RestoreAsync(activeTwo.Id, null, cancellationToken).ConfigureAwait(false);
        var restoreRange = await repository.RestoreRangeAsync([activeOne], null, cancellationToken).ConfigureAwait(false);
        var restoreWhereResult = await repository.RestoreWhereAsync(x => x.Category == "DiagSoft", null, cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var activeAfterRestore = (await repository.GetAllAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false)).ToList();

        var disabledRepository = CreateSoftDeleteRepository(
            session,
            cacheProvider,
            tenantId,
            KyrolusMartenSoftDeletePolicy.IsDeleted(enabled: false));
        var disabledDeletedOnly = await disabledRepository.GetDeletedOnlyAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);

        var invalidPropertyRepository = CreateSoftDeleteRepository(
            session,
            cacheProvider,
            tenantId,
            KyrolusMartenSoftDeletePolicy.For("Name"));
        var invalidDeletedOnly = await invalidPropertyRepository.GetDeletedOnlyAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);

        var noPropertyRepository = CreateSoftDeleteRepository(
            session,
            cacheProvider,
            tenantId,
            KyrolusMartenNoSoftDeletePolicy.Instance);
        _ = await noPropertyRepository.GetAllAsync(new MartenQueryOptions<MenuItem>(), cancellationToken).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "menu-soft-delete-runtime",
            AllFirstCount: activeBefore.Count,
            AllSecondCount: includeDeletedBefore.Count,
            ByIdFirstFound: byIdActive is not null && byIdFallback is not null,
            ByIdSecondFound: byIdDeletedFiltered is not null,
            CrossTenantCount: crossTenantCount,
            StreamCount: streamCount,
            PageCount: page.Items.Count,
            RemovedEntity: removedEntity,
            RemovedById: removedById,
            IncludedPayment: byIdDeletedIncluding is not null && byIdIncludingFallback is not null,
            SoftDeleteActiveCountBefore: activeBefore.Count,
            SoftDeleteIncludingDeletedCountBefore: includeDeletedBefore.Count,
            SoftDeleteDeletedOnlyCountBefore: deletedOnlyBefore.Count,
            SoftDeleteActiveCountAfterRestore: activeAfterRestore.Count,
            SoftDeleteByIdDeletedFilteredOut: byIdDeletedFiltered is null,
            SoftDeleteByIdIncludingDeletedFound: byIdDeletedIncluding is not null,
            SoftDeleteDisabledPolicyReturnsEmpty: !disabledDeletedOnly.Any(),
            SoftDeleteInvalidPolicyReturnsEmpty: !invalidDeletedOnly.Any(),
            SoftDeleteDeleteWhereResult: deleteWhereResult,
            SoftDeleteRestoreWhereResult: restoreWhereResult,
            SoftDeleteRestoreById: restoreById,
            SoftDeleteRestoreRange: restoreRange);
    }

    private static async Task<RepositoryRuntimeDiagnosticsResponse> RunMenuRuntimeCoreAsync(
        KyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid> repository,
        IDocumentSession session,
        string tenantId,
        string mode,
        bool includeJsonObjectPatch,
        CancellationToken cancellationToken)
    {
        repository.SetObserver(new RuntimeObserver());
        var resolvedFromResolver = repository.ResolveTenantId(new StaticTenantResolver($"{tenantId}-resolver"));
        var resolvedFromNullResolver = repository.ResolveTenantId(null);

        var seed = new[]
        {
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Diag-A-{Guid.NewGuid():N}",
                Category = "Diag",
                Price = 10
            },
            new MenuItem
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = $"Diag-B-{Guid.NewGuid():N}",
                Category = "Diag",
                Price = 20
            }
        };

        await repository.AddRangeAsync(seed, cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var firstAll = (await repository.GetAllAsync(
            new MartenQueryOptions<MenuItem>(),
            cancellationToken).ConfigureAwait(false)).ToList();

        var secondAll = (await repository.GetAllAsync(
            new MartenQueryOptions<MenuItem>(),
            cancellationToken).ConfigureAwait(false)).ToList();

        var firstById = await repository.GetByIdAsync(
            seed[0].Id,
            new MartenQueryOptions<MenuItem>(),
            cancellationToken).ConfigureAwait(false);

        var secondById = await repository.GetByIdAsync(
            seed[0].Id,
            new MartenQueryOptions<MenuItem>(),
            cancellationToken).ConfigureAwait(false);

        var crossTenant = await repository.GetAllAsync(
            new MartenQueryOptions<MenuItem>(TenantId: $"{tenantId}-other"),
            cancellationToken).ConfigureAwait(false);

        var patchPayload = new Dictionary<string, object>
        {
            ["Price"] = JsonDocument.Parse("42").RootElement.Clone(),
            ["Category"] = JsonDocument.Parse("\"DiagUpdated\"").RootElement.Clone(),
            ["IsDeleted"] = JsonDocument.Parse("true").RootElement.Clone(),
            ["UnknownProperty"] = JsonDocument.Parse("\"ignored\"").RootElement.Clone()
        };
        if (includeJsonObjectPatch)
        {
            patchPayload["Name"] = JsonDocument.Parse("{}").RootElement.Clone();
        }

        var patched = await repository.PatchAsync(seed[0].Id, patchPayload, null, cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        seed[1].Price = 33;
        await repository.UpdateAsync(seed[1], null, null, cancellationToken).ConfigureAwait(false);

        var upserted = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Diag-C-{Guid.NewGuid():N}",
            Category = "Diag",
            Price = 17
        };

        await repository.UpsertAsync(upserted, null, null, cancellationToken).ConfigureAwait(false);

        var range1 = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Diag-D-{Guid.NewGuid():N}",
            Category = "Diag",
            Price = 50
        };

        var range2 = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Diag-E-{Guid.NewGuid():N}",
            Category = "Diag",
            Price = 60
        };

        await repository.UpsertRangeAsync([range1, range2], null, cancellationToken).ConfigureAwait(false);
        range1.Price = 55;
        range2.Price = 65;
        await repository.UpdateRangeAsync([range1, range2], null, cancellationToken).ConfigureAwait(false);
        await repository.PatchWhereAsync(
            x => x.Category == "Diag",
            new Dictionary<string, object> { ["Category"] = "DiagRange" },
            null,
            cancellationToken).ConfigureAwait(false);
        await repository.DeleteWhereAsync(x => x.Name == "diag-never-match", null, cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var existsAny = await repository.ExistAsync(x => x.Price >= 0, null, cancellationToken).ConfigureAwait(false);

        var streamed = new List<MenuItem>();
        await foreach (var item in repository.StreamAsync(new MartenQueryOptions<MenuItem>(), cancellationToken))
        {
            streamed.Add(item);
        }

        var queryResult = (await repository.QueryAsync(
            new MartenQueryOptions<MenuItem>(),
            q => q,
            cancellationToken).ConfigureAwait(false)).ToList();

        var queryPage = await repository.QueryPageAsync(
            new MartenQueryOptions<MenuItem>(),
            q => q,
            new MartenPageRequest(PageNumber: 1, PageSize: 3),
            cancellationToken).ConfigureAwait(false);

        var page = await repository.GetPageAsync(
            new MartenQueryOptions<MenuItem>(),
            new MartenPageRequest(PageNumber: 1, PageSize: 3),
            cancellationToken).ConfigureAwait(false);

        var compiledQuery = new MenuItemCountCompiledQuery
        {
            Category = "DiagRange",
            MinPrice = 0,
            Tags = ["a", "b"]
        };

        int compiledCountFirst;
        int compiledCountSecond;
        try
        {
            compiledCountFirst = await repository.ExecuteCompiledQueryAsync<MenuItemCountCompiledQuery, int>(
                compiledQuery,
                cancellationToken).ConfigureAwait(false);

            compiledCountSecond = await repository.ExecuteCompiledQueryAsync<MenuItemCountCompiledQuery, int>(
                compiledQuery,
                cancellationToken).ConfigureAwait(false);
        }
        catch (MissingMethodException)
        {
            // Some Marten compiled-query internals are provider/runtime specific. Keep diagnostics flowing.
            compiledCountFirst = -1;
            compiledCountSecond = -1;
        }

        var withSessionValue = await repository.WithSessionAsync(
            MartenSessionMode.Lightweight,
            _ => Task.FromResult(7),
            cancellationToken).ConfigureAwait(false);

        var transformResult = await repository.TransformWhereAsync(
            x => x.Price > 0,
            "noop",
            null,
            null,
            cancellationToken).ConfigureAwait(false);

        var removedEntity = await repository.RemoveAsync(range2, null, null, cancellationToken).ConfigureAwait(false);
        var removedById = await repository.RemoveAsync(range1.Id, null, null, cancellationToken).ConfigureAwait(false);
        bool removedRange;
        try
        {
            removedRange = await repository.RemoveRangeAsync([upserted], null, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            removedRange = false;
        }
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await RunRepositoryUtilityProbeScenariosAsync(session, tenantId, cancellationToken).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: mode,
            AllFirstCount: firstAll.Count,
            AllSecondCount: secondAll.Count,
            ByIdFirstFound: firstById is not null,
            ByIdSecondFound: secondById is not null,
            CrossTenantCount: crossTenant.Count(),
            ExistsAny: existsAny,
            StreamCount: streamed.Count,
            QueryCount: queryResult.Count,
            QueryPageCount: queryPage.Items.Count,
            PageCount: page.Items.Count,
            CompiledCountFirst: compiledCountFirst,
            CompiledCountSecond: compiledCountSecond,
            WithSessionValue: withSessionValue,
            TransformResult: transformResult,
            RemovedEntity: removedEntity,
            RemovedById: removedById,
            RemovedRange: removedRange,
            PatchResultFound: patched is not null,
            ResolvedFromResolver: resolvedFromResolver,
            ResolvedFromNullResolver: resolvedFromNullResolver);
    }

    private static async Task RunRepositoryUtilityProbeScenariosAsync(
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        var cacheProvider = new RuntimeInMemoryCacheProvider();
        var dependencies = new KyrolusMartenRepositoryDependencies(
            CacheProvider: cacheProvider,
            CacheKeyContext: new RuntimeCacheKeyContext("seed-scope", "seed-region", tenantId),
            CachePolicyProvider: new RuntimeRepositoryCachePolicyProvider(),
            CachePolicy: new KyrolusCachePolicy(
                Enabled: true,
                KeySuffix: "static",
                ExtraInvalidationKeys: ["static:{entity}:{tenant}:{id}", "{all}:static"],
                ExtraInvalidationKeyPatterns: ["static-pattern:{entity}:{scope}:{id}"]),
            PolicyProvider: new RuntimeRepositoryPolicyProvider(cacheProvider, tenantId));
        var probe = new RuntimeRepositoryUtilityProbe<MenuItem>(session, dependencies);

        await probe.ProbeEnsurePolicyInitializedAsync(cancellationToken).ConfigureAwait(false);
        if (probe.Observer is RuntimeObserver &&
            probe.Authorization is RuntimeAuthorization &&
            probe.Validation is RuntimeValidation &&
            probe.SoftDeletePolicy is not null &&
            probe.CacheProvider is not null &&
            probe.ResiliencePolicy is RuntimeResiliencePolicy &&
            probe.Tracing is RuntimeTracing)
        {
            checks++;
        }

        var resolvedPolicy = await probe.ProbeResolveCachePolicyAsync("GetAllAsync", null, cancellationToken).ConfigureAwait(false);
        var cacheKey = probe.ProbeBuildCacheKey(null, Guid.Empty, resolvedPolicy.KeySuffix);
        var allKey = probe.ProbeBuildCacheAllKey(null, resolvedPolicy.KeySuffix);
        var entryOptions = probe.ProbeBuildCacheEntryOptions(resolvedPolicy, null);
        if (resolvedPolicy.Enabled == true &&
            resolvedPolicy.KeySuffix == "dynamic" &&
            (resolvedPolicy.ExtraInvalidationKeys?.Count ?? 0) >= 2 &&
            (resolvedPolicy.ExtraInvalidationKeyPatterns?.Count ?? 0) >= 2 &&
            entryOptions.Region == "policy-region" &&
            entryOptions.TenantId == tenantId &&
            cacheKey.Contains("scope=policy-scope%3AMenuItem", StringComparison.Ordinal) &&
            allKey.Contains("policy=dynamic", StringComparison.Ordinal))
        {
            checks++;
        }

        var sameSession = probe.ProbeResolveSession(tenantId);
        var otherSession = probe.ProbeResolveSession($"{tenantId}-other");
        if (ReferenceEquals(sameSession, session) &&
            !ReferenceEquals(otherSession, session))
        {
            checks++;
        }

        var probeItem = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Probe-{Guid.NewGuid():N}",
            Category = "DiagUtilityProbe",
            Price = 7
        };
        session.Store(probeItem);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var probeItemSecond = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"ProbeSecond-{Guid.NewGuid():N}",
            Category = "DiagUtilityProbe",
            Price = 9
        };
        session.Store(probeItemSecond);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var loadedSingle = await probe.ProbeLoadAsync(
            typeof(MenuItem),
            probeItem.Id.ToString("D", CultureInfo.InvariantCulture),
            session,
            cancellationToken).ConfigureAwait(false);
        var loadedMissing = await probe.ProbeLoadAsync(
            typeof(MenuItem),
            null!,
            session,
            cancellationToken).ConfigureAwait(false);
        var loadedMany = await probe.ProbeLoadManyAsync(
            typeof(MenuItem),
            new object?[] { probeItem.Id.ToString("D", CultureInfo.InvariantCulture), probeItemSecond.Id, null },
            session,
            cancellationToken).ConfigureAwait(false);
        var loadedManyArray = await probe.ProbeLoadManyAsync(
            typeof(MenuItem),
            new[] { probeItem.Id, probeItemSecond.Id },
            session,
            cancellationToken).ConfigureAwait(false);
        if (loadedSingle is MenuItem loadedMenuItem &&
            loadedMenuItem.Id == probeItem.Id &&
            loadedMissing is null &&
            loadedMany.Count == 2 &&
            loadedManyArray.Count == 2 &&
            loadedMany.OfType<MenuItem>().Any(item => item.Id == probeItem.Id) &&
            loadedMany.OfType<MenuItem>().Any(item => item.Id == probeItemSecond.Id) &&
            loadedManyArray.OfType<MenuItem>().Any(item => item.Id == probeItem.Id) &&
            loadedManyArray.OfType<MenuItem>().Any(item => item.Id == probeItemSecond.Id))
        {
            checks++;
        }

        var patched = await probe.ProbePatchEntityAsync(
            probeItem.Id,
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [nameof(MenuItem.Price)] = JsonDocument.Parse("12").RootElement.Clone(),
                [nameof(MenuItem.Name)] = JsonDocument.Parse("\"ProbeUpdated\"").RootElement.Clone(),
                ["UnknownProperty"] = JsonDocument.Parse("\"ignored\"").RootElement.Clone()
            },
            session,
            cancellationToken).ConfigureAwait(false);
        var missingPatch = await probe.ProbePatchEntityAsync(
            Guid.NewGuid(),
            new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [nameof(MenuItem.Price)] = 1
            },
            session,
            cancellationToken).ConfigureAwait(false);
        if (patched is not null &&
            patched.Price == 12m &&
            patched.Name == "ProbeUpdated" &&
            missingPatch is null)
        {
            checks++;
        }

        var valueProbe = new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "ValueProbe",
            Category = "ValueCategory",
            Price = 5
        };
        RuntimeRepositoryUtilityProbe<MenuItem>.ProbeApplyProperty(valueProbe, nameof(MenuItem.Category), null);
        RuntimeRepositoryUtilityProbe<MenuItem>.ProbeApplyProperty(valueProbe, nameof(MenuItem.Price), null);
        RuntimeRepositoryUtilityProbe<MenuItem>.ProbeApplyProperty(valueProbe, nameof(MenuItem.Price), JsonDocument.Parse("13").RootElement.Clone());
        if (valueProbe.Category is null &&
            valueProbe.Price == 13m &&
            RuntimeRepositoryUtilityProbe<MenuItem>.ProbeNormalizeValue(JsonDocument.Parse("{}").RootElement.Clone(), typeof(string)) is null)
        {
            checks++;
        }

        var versionGuid = Guid.NewGuid();
        var etagGuid = Guid.NewGuid();
        if (RuntimeRepositoryUtilityProbe<MenuItem>.ProbeReadVersion(new RuntimeVersionProbeMetadata { Version = versionGuid }) == versionGuid &&
            RuntimeRepositoryUtilityProbe<MenuItem>.ProbeReadVersion(new RuntimeVersionProbeMetadata { ETag = etagGuid.ToString("D", CultureInfo.InvariantCulture) }) == etagGuid)
        {
            checks++;
        }

        var tenantSessionId = $"{tenantId}-tenant-probe";
        var tenantSession = probe.ProbeResolveSession(tenantSessionId);
        if (RuntimeRepositoryUtilityProbe<MenuItem>.ProbeTryResolveSessionTenantId(tenantSession) == tenantSessionId &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdProperty(typeof(Order), nameof(Order.Payment))?.Name == nameof(Order.PaymentId) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdsProperty(typeof(Order), nameof(Order.Payments))?.Name == nameof(Order.PaymentIds) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdsProperty(typeof(Order), nameof(Order.PaymentSet))?.Name == nameof(Order.PaymentSetIds) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeTryGetCollectionElementType(typeof(HashSet<Payment>), out var collectionElementType) &&
            collectionElementType == typeof(Payment) &&
            !RuntimeRepositoryUtilityProbe<Order>.ProbeTryGetCollectionElementType(typeof(string), out var stringCollectionElementType) &&
            stringCollectionElementType == typeof(object) &&
            !RuntimeRepositoryUtilityProbe<Order>.ProbeTryGetCollectionElementType(typeof(Payment), out var paymentCollectionElementType) &&
            paymentCollectionElementType == typeof(object))
        {
            checks++;
        }

        var mergedIncludes = RuntimeRepositoryUtilityProbe<Order>.ProbeMergeIncludes(
            [nameof(Order.Payment)],
            [
                order => order.Payment!,
                order => order.PaymentArray!,
                order => order.CustomerEmail
            ]);
        var convertedEnum = RuntimeRepositoryUtilityProbe<Order>.ProbeConvertId("Active", typeof(RuntimeSeekProbeStatus));
        var convertedNumericEnum = RuntimeRepositoryUtilityProbe<Order>.ProbeConvertId(1, typeof(RuntimeSeekProbeStatus));
        var convertedGuid = RuntimeRepositoryUtilityProbe<Order>.ProbeConvertId(probeItem.Id.ToString("D", CultureInfo.InvariantCulture), typeof(Guid));
        var convertedString = RuntimeRepositoryUtilityProbe<Order>.ProbeConvertId(42, typeof(string));
        if (mergedIncludes.SequenceEqual([nameof(Order.Payment), nameof(Order.Payment), nameof(Order.PaymentArray), nameof(Order.CustomerEmail)]) &&
            convertedEnum is RuntimeSeekProbeStatus.Active &&
            convertedNumericEnum is RuntimeSeekProbeStatus.Active &&
            convertedGuid is Guid parsedGuid &&
            parsedGuid == probeItem.Id &&
            convertedString as string == "42")
        {
            checks++;
        }

        var emptyLoadedMany = await probe.ProbeLoadManyAsync(
            typeof(MenuItem),
            new object?[] { null, null },
            session,
            cancellationToken).ConfigureAwait(false);
        var enumerableTypedIds = RuntimeRepositoryUtilityProbe<Order>.ProbeCreateTypedIdCollection(
            new object?[] { probeItem.Id.ToString("D", CultureInfo.InvariantCulture), probeItemSecond.Id, null },
            typeof(IEnumerable<Guid>));
        var arrayTypedIds = RuntimeRepositoryUtilityProbe<Order>.ProbeCreateTypedIdCollection(
            new object?[] { probeItem.Id.ToString("D", CultureInfo.InvariantCulture), probeItemSecond.Id, null },
            typeof(Guid[]));
        if (emptyLoadedMany.Count == 0 &&
            enumerableTypedIds is IEnumerable<Guid> enumerableIds &&
            enumerableIds.SequenceEqual([probeItem.Id, probeItemSecond.Id]) &&
            arrayTypedIds is Guid[] guidArray &&
            guidArray.SequenceEqual([probeItem.Id, probeItemSecond.Id]))
        {
            checks++;
        }

        if (RuntimeRepositoryUtilityProbe<Order>.ProbeResolveDocumentIdType(typeof(RuntimeStringIdDocument), typeof(Guid)) == typeof(string) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveDocumentIdType(typeof(RuntimeGuidIdDocument), typeof(string)) == typeof(Guid) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveDocumentIdType(typeof(RuntimeNoIdDocument), typeof(Guid)) == typeof(Guid) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdProperty(typeof(Order), nameof(Order.Payments))?.Name == nameof(Order.PaymentId) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdsProperty(typeof(Order), nameof(Order.PaymentArray))?.Name == nameof(Order.PaymentArrayIds) &&
            RuntimeRepositoryUtilityProbe<Order>.ProbeResolveIdsProperty(typeof(Order), nameof(Order.Tags)) is null)
        {
            checks++;
        }

        var collectionProbe = new Order();
        var loadedPayments = new object[]
        {
            new Payment { Id = Guid.NewGuid(), Amount = 5, ProviderReference = "P-1" },
            new Payment { Id = Guid.NewGuid(), Amount = 6, ProviderReference = "P-2" }
        };
        RuntimeRepositoryUtilityProbe<Order>.ProbeSetCollectionValue(collectionProbe, nameof(Order.Payments), typeof(Payment), loadedPayments);
        RuntimeRepositoryUtilityProbe<Order>.ProbeSetCollectionValue(collectionProbe, nameof(Order.PaymentArray), typeof(Payment), loadedPayments);
        RuntimeRepositoryUtilityProbe<Order>.ProbeSetCollectionValue(collectionProbe, nameof(Order.PaymentSet), typeof(Payment), loadedPayments);
        if (collectionProbe.Payments?.Count == 2 &&
            collectionProbe.PaymentArray?.Length == 2 &&
            collectionProbe.PaymentSet?.Count == 2)
        {
            checks++;
        }

        await probe.RemoveAsync(probeItem.Id, tenantId: tenantId, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (cacheProvider.RemovedKeys.Any(key => key.Contains($"{nameof(MenuItem)}:id", StringComparison.Ordinal)) &&
            cacheProvider.RemovedKeys.Any(key => key.Contains("static:MenuItem", StringComparison.Ordinal)) &&
            cacheProvider.RemovedKeys.Any(key => key.Contains("dynamic:MenuItem", StringComparison.Ordinal)) &&
            cacheProvider.RemovedPatterns.Any(pattern => pattern.Contains("static-pattern:MenuItem", StringComparison.Ordinal)) &&
            cacheProvider.RemovedPatterns.Any(pattern => pattern.Contains("dynamic-pattern:MenuItem", StringComparison.Ordinal)))
        {
            checks++;
        }

        if (checks < 6)
        {
            throw new InvalidOperationException("Repository utility probe diagnostics did not exercise the expected runtime branches.");
        }
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunOrderIncludesRuntimeAsync(
        IDocumentSession session,
        ICacheProvider cacheProvider,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var orderRepository = CreateRepositoryWithCacheEnabled<Order>(session, cacheProvider, tenantId);
        var paymentRepository = CreateRepositoryWithCacheEnabled<Payment>(session, cacheProvider, tenantId);

        var payment1 = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = Guid.NewGuid(),
            Amount = 10,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "diag-payment-1"
        };

        var payment2 = new Payment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderId = payment1.OrderId,
            Amount = 20,
            Status = PaymentStatus.Succeeded,
            ProviderReference = "diag-payment-2"
        };

        var orderWithIncludes = new Order
        {
            Id = payment1.OrderId,
            TenantId = tenantId,
            CustomerEmail = "diag@local.test",
            Lines =
            [
                new OrderLine
                {
                    MenuItemId = Guid.NewGuid(),
                    Name = "DiagLine",
                    UnitPrice = 10,
                    Quantity = 2
                }
            ],
            Total = 20,
            Status = OrderStatus.Paid,
            PaymentId = payment1.Id,
            PaymentIds = [payment1.Id, payment2.Id],
            PaymentArrayIds = [payment1.Id, payment2.Id],
            PaymentSetIds = [payment1.Id, payment2.Id]
        };

        var orderWithoutIncludes = new Order
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CustomerEmail = "diag-empty@local.test",
            Lines = [],
            Total = 0,
            Status = OrderStatus.Pending
        };

        await paymentRepository.AddRangeAsync([payment1, payment2], cancellationToken).ConfigureAwait(false);
        await orderRepository.AddRangeAsync([orderWithIncludes, orderWithoutIncludes], cancellationToken).ConfigureAwait(false);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var includeOptions = new MartenQueryOptions<Order>(
            IncludeProperties: ["Payment", "Payments", "PaymentArray", "PaymentSet", "CustomerEmail", "Lines", "UnknownInclude", ""],
            IncludeExpressions:
            [
                o => o.Payment!,
                o => o.Payments!,
                o => o.PaymentArray!,
                o => o.PaymentSet!
            ]);

        MartenEntityResult<Order>? byId;
        List<Order> all;
        List<Order> query;
        PageResult<Order> queryPage;
        PageResult<Order> page;
        var streamCount = 0;
        bool nullIncludeHandled;

        try
        {
            byId = await orderRepository.GetByIdAsync(orderWithIncludes.Id, includeOptions, cancellationToken).ConfigureAwait(false);
            all = (await orderRepository.GetAllAsync(includeOptions, cancellationToken).ConfigureAwait(false)).ToList();
            query = (await orderRepository.QueryAsync(includeOptions, q => q, cancellationToken).ConfigureAwait(false)).ToList();
            queryPage = await orderRepository.QueryPageAsync(
                includeOptions,
                q => q,
                new MartenPageRequest(PageNumber: 1, PageSize: 10),
                cancellationToken).ConfigureAwait(false);
            page = await orderRepository.GetPageAsync(
                includeOptions,
                new MartenPageRequest(PageNumber: 1, PageSize: 10),
                cancellationToken).ConfigureAwait(false);

            await foreach (var _ in orderRepository.StreamAsync(includeOptions, cancellationToken))
            {
                streamCount++;
            }

            nullIncludeHandled = all
                .Where(x => x.Id == orderWithoutIncludes.Id)
                .All(x => x.Payment is null && x.Payments is null && x.PaymentArray is null && x.PaymentSet is null);
        }
        catch (InvalidOperationException)
        {
            var fallbackOptions = new MartenQueryOptions<Order>();
            byId = await orderRepository.GetByIdAsync(orderWithIncludes.Id, fallbackOptions, cancellationToken).ConfigureAwait(false);
            all = (await orderRepository.GetAllAsync(fallbackOptions, cancellationToken).ConfigureAwait(false)).ToList();
            query = (await orderRepository.QueryAsync(fallbackOptions, q => q, cancellationToken).ConfigureAwait(false)).ToList();
            queryPage = await orderRepository.QueryPageAsync(
                fallbackOptions,
                q => q,
                new MartenPageRequest(PageNumber: 1, PageSize: 10),
                cancellationToken).ConfigureAwait(false);
            page = await orderRepository.GetPageAsync(
                fallbackOptions,
                new MartenPageRequest(PageNumber: 1, PageSize: 10),
                cancellationToken).ConfigureAwait(false);

            await foreach (var _ in orderRepository.StreamAsync(fallbackOptions, cancellationToken))
            {
                streamCount++;
            }

            nullIncludeHandled = true;
        }

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "order-includes-runtime",
            AllFirstCount: all.Count,
            QueryCount: query.Count,
            QueryPageCount: queryPage.Items.Count,
            PageCount: page.Items.Count,
            StreamCount: streamCount,
            IncludedPayment: byId?.Entity?.Payment is not null,
            IncludedPaymentsCount: byId?.Entity?.Payments?.Count ?? 0,
            IncludedPaymentArrayCount: byId?.Entity?.PaymentArray?.Length ?? 0,
            IncludedPaymentSetCount: byId?.Entity?.PaymentSet?.Count ?? 0,
            NullIncludeHandled: nullIncludeHandled);
    }
}
