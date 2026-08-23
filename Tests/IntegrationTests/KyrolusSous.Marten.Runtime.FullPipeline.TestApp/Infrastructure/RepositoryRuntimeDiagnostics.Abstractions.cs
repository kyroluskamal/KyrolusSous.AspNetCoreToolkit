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
    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunAbstractionsRuntimeAsync(
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        session.Store(new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"Abstractions-{Guid.NewGuid():N}",
            Category = "DiagAbstractions",
            Price = 1
        });
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dbProbeCount = await session.Query<MenuItem>()
            .Where(x => x.TenantId == tenantId && x.Category == "DiagAbstractions")
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var authorizationChecks = await RunAuthorizationScenariosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var validationChecks = await RunValidationScenariosAsync(cancellationToken).ConfigureAwait(false);
        var tracingChecks = await RunTracingScenariosAsync(cancellationToken).ConfigureAwait(false);
        var observerChecks = await RunObserverScenariosAsync(cancellationToken).ConfigureAwait(false);
        var resilienceChecks = await RunResilienceScenariosAsync(cancellationToken).ConfigureAwait(false);
        var specificationChecks = await RunSpecificationScenariosAsync(session, tenantId, cancellationToken).ConfigureAwait(false);
        var queryPrimitiveChecks = await RunQueryPrimitiveScenariosAsync(cancellationToken).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "abstractions-runtime",
            AuthorizationChecks: authorizationChecks,
            ValidationChecks: validationChecks,
            TracingChecks: tracingChecks,
            ObserverChecks: observerChecks,
            ResilienceChecks: resilienceChecks,
            SpecificationChecks: specificationChecks,
            QueryPrimitiveChecks: queryPrimitiveChecks,
            DbProbeCount: dbProbeCount);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunRuntimeInfrastructureAsync(
        IDocumentStore store,
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        session.Store(new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"RuntimeInfra-{Guid.NewGuid():N}",
            Category = "DiagRuntimeInfra",
            Price = 1
        });
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dbProbeCount = await session.Query<MenuItem>()
            .Where(x => x.TenantId == tenantId && x.Category == "DiagRuntimeInfra")
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var sagaChecks = await RunBestEffortAsync(() => RunSagaScenariosAsync(session, cancellationToken)).ConfigureAwait(false);
        var eventStoreChecks = await RunBestEffortAsync(() => RunEventStoreScenariosAsync(session, cancellationToken)).ConfigureAwait(false);
        var projectionManagerChecks = await RunBestEffortAsync(() => RunProjectionManagerScenariosAsync(store, cancellationToken)).ConfigureAwait(false);
        var projectionOrchestratorChecks = await RunBestEffortAsync(() => RunProjectionOrchestratorScenariosAsync(store, cancellationToken)).ConfigureAwait(false);
        var runtimeRegistrationChecks = await RunBestEffortAsync(() => RunRuntimeRegistrationScenariosAsync(store, cancellationToken)).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "runtime-infrastructure",
            SagaChecks: sagaChecks,
            EventStoreChecks: eventStoreChecks,
            ProjectionManagerChecks: projectionManagerChecks,
            ProjectionOrchestratorChecks: projectionOrchestratorChecks,
            RuntimeRegistrationChecks: runtimeRegistrationChecks,
            DbProbeCount: dbProbeCount);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunCqrsHandlersRuntimeAsync(
        IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        session.Store(new MenuItem
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = $"CqrsRuntime-{Guid.NewGuid():N}",
            Category = "DiagCqrsHandlers",
            Price = 1
        });
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dbProbeCount = await session.Query<MenuItem>()
            .Where(x => x.TenantId == tenantId && x.Category == "DiagCqrsHandlers")
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var cqrsHandlerChecks = await RunBestEffortAsync(() => RunCqrsHandlerScenariosAsync(unitOfWork, session, tenantId, cancellationToken)).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "cqrs-handlers-runtime",
            CqrsHandlerChecks: cqrsHandlerChecks,
            DbProbeCount: dbProbeCount);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunEndpointKitCoreRuntimeAsync(
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;
        checks += await RunFieldSelectionScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunEnvelopeScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunHateoasScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunOpenApiSchemaProviderScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunOpenApiMetadataScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunDefaultRouteMapperScenariosAsync(cancellationToken).ConfigureAwait(false);
        checks += await RunEndpointPolicyFilterScenariosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        checks += await RunEndpointCacheRegistryScenariosAsync(tenantId, cancellationToken).ConfigureAwait(false);
        checks += await RunIdempotencyStoreScenariosAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "endpointkit-core-runtime",
            EndpointKitCoreChecks: checks,
            DbProbeCount: 0);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunValidationRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = await RunValidationRuntimeScenariosAsync(cancellationToken).ConfigureAwait(false);
        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "validation-runtime",
            ValidationRuntimeChecks: checks,
            DbProbeCount: 0);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunExceptionHandlingRuntimeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var checks = await RunExceptionHandlingScenariosAsync(serviceProvider, cancellationToken).ConfigureAwait(false);
        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "exception-handling-runtime",
            ExceptionHandlingChecks: checks,
            DbProbeCount: 0);
    }

    public static async Task<RepositoryRuntimeDiagnosticsResponse> RunCacheAbstractionsRuntimeAsync(
        CancellationToken cancellationToken)
    {
        var checks = await RunCacheAbstractionsScenariosAsync(cancellationToken).ConfigureAwait(false);
        return new RepositoryRuntimeDiagnosticsResponse(
            Mode: "cache-abstractions-runtime",
            CacheAbstractionsChecks: checks,
            DbProbeCount: 0);
    }

    private static async Task<int> RunAuthorizationScenariosAsync(string tenantId, CancellationToken cancellationToken)
    {
        var checks = 0;

        if (await KyrolusMartenAllowAllAuthorization.Instance.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        if (!await KyrolusMartenDenyAllAuthorization.Instance.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var delegateAuthorization = new KyrolusMartenDelegateAuthorization((operation, _, _) => Task.FromResult(operation == "write"));
        if (await delegateAuthorization.AuthorizeAsync("write", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await delegateAuthorization.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var whitelist = new KyrolusMartenOperationWhitelistAuthorization(["read", "write"], allowWhenUnknown: true);
        if (await whitelist.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await whitelist.AuthorizeAsync(string.Empty, null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var blacklist = new KyrolusMartenOperationBlacklistAuthorization(["delete"], allowWhenUnknown: false);
        if (!await blacklist.AuthorizeAsync("delete", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await blacklist.AuthorizeAsync(string.Empty, null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var prefix = new KyrolusMartenOperationPrefixAuthorization(["menu.", "order."], allowWhenUnknown: false);
        if (await prefix.AuthorizeAsync("menu.read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await prefix.AuthorizeAsync("audit.read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var map = new KyrolusMartenOperationMapAuthorization(
            new Dictionary<string, IKyrolusMartenAuthorization>(StringComparer.OrdinalIgnoreCase)
            {
                ["read"] = KyrolusMartenAllowAllAuthorization.Instance
            },
            KyrolusMartenDenyAllAuthorization.Instance);
        if (await map.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await map.AuthorizeAsync("update", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var all = new KyrolusMartenCompositeAllAuthorization([KyrolusMartenAllowAllAuthorization.Instance, delegateAuthorization], allowWhenEmpty: false);
        if (await all.AuthorizeAsync("write", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await all.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var any = new KyrolusMartenCompositeAnyAuthorization([KyrolusMartenDenyAllAuthorization.Instance, delegateAuthorization], allowWhenEmpty: true);
        if (await any.AuthorizeAsync("write", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await any.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        var emptyAny = new KyrolusMartenCompositeAnyAuthorization([], allowWhenEmpty: true);
        if (await emptyAny.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var targetType = new KyrolusMartenTargetTypeAuthorization(
            new Dictionary<Type, IKyrolusMartenAuthorization>
            {
                [typeof(string)] = KyrolusMartenAllowAllAuthorization.Instance,
                [typeof(MenuItem)] = KyrolusMartenDenyAllAuthorization.Instance
            },
            fallback: KyrolusMartenAllowAllAuthorization.Instance,
            allowWhenNoTarget: false);

        if (!await targetType.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await targetType.AuthorizeAsync("read", "value", cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await targetType.AuthorizeAsync("read", new MenuItem(), cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await targetType.AuthorizeAsync("read", 7, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var tenantMatch = new KyrolusMartenTenantMatchAuthorization(
            new StaticTenantResolver(tenantId),
            target => target as string,
            allowWhenUnknown: false);
        if (await tenantMatch.AuthorizeAsync("read", tenantId, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await tenantMatch.AuthorizeAsync("read", $"{tenantId}-other", cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (!await tenantMatch.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var context = new DiagnosticsAuthorizationContext(
            Roles: ["admin", "auditor"],
            Permissions: ["menu.read", "menu.write"]);

        var roleAny = new KyrolusMartenRoleAuthorization(["admin"]);
        var roleAll = new KyrolusMartenRoleAuthorization(["admin", "auditor"], requireAll: true);
        var roleNoContext = new KyrolusMartenRoleAuthorization(["manager"], allowWhenNoContext: true);
        if (await roleAny.AuthorizeAsync("read", context, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await roleAll.AuthorizeAsync("read", context, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await roleNoContext.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        var permissionAny = new KyrolusMartenPermissionAuthorization(["menu.read"]);
        var permissionAll = new KyrolusMartenPermissionAuthorization(["menu.read", "menu.write"], requireAll: true);
        var permissionNoContext = new KyrolusMartenPermissionAuthorization(["menu.read"], allowWhenNoContext: true);
        if (await permissionAny.AuthorizeAsync("read", context, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await permissionAll.AuthorizeAsync("read", context, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }
        if (await permissionNoContext.AuthorizeAsync("read", null, cancellationToken).ConfigureAwait(false))
        {
            checks++;
        }

        return checks;
    }

    private static async Task<int> RunValidationScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        await KyrolusMartenNoopValidation.Instance.ValidateAsync("noop", null, cancellationToken).ConfigureAwait(false);
        checks++;

        var delegateValidation = new KyrolusMartenDelegateValidation((_, payload, _) =>
            payload is string ? Task.CompletedTask : Task.FromException(new InvalidOperationException("Payload must be string.")));
        await delegateValidation.ValidateAsync("delegate", "ok", cancellationToken).ConfigureAwait(false);
        checks++;

        var payloadNotNull = new KyrolusMartenPayloadNotNullValidation();
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => payloadNotNull.ValidateAsync("nonnull", null, cancellationToken)).ConfigureAwait(false);
        checks++;
        await payloadNotNull.ValidateAsync("nonnull", new object(), cancellationToken).ConfigureAwait(false);
        checks++;

        var payloadType = new KyrolusMartenPayloadTypeValidation([typeof(string)], allowNull: false, allowDerived: false);
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => payloadType.ValidateAsync("type", null, cancellationToken)).ConfigureAwait(false);
        checks++;
        await payloadType.ValidateAsync("type", "ok", cancellationToken).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => payloadType.ValidateAsync("type", 5, cancellationToken)).ConfigureAwait(false);
        checks++;

        var stringLength = new KyrolusMartenStringLengthValidation(minLength: 2, maxLength: 4, allowNonString: false);
        await stringLength.ValidateAsync("string", "abcd", cancellationToken).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => stringLength.ValidateAsync("string", "a", cancellationToken)).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => stringLength.ValidateAsync("string", "abcdef", cancellationToken)).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => stringLength.ValidateAsync("string", 1, cancellationToken)).ConfigureAwait(false);
        checks++;

        var collectionCount = new KyrolusMartenCollectionCountValidation(minCount: 1, maxCount: 2, requireCollection: true);
        await collectionCount.ValidateAsync("collection", new[] { 1 }, cancellationToken).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => collectionCount.ValidateAsync("collection", Array.Empty<int>(), cancellationToken)).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => collectionCount.ValidateAsync("collection", new[] { 1, 2, 3 }, cancellationToken)).ConfigureAwait(false);
        checks++;

        var validatable = new KyrolusMartenValidatablePayloadValidation(requireValidatable: true);
        await validatable.ValidateAsync("validatable", new DiagnosticsAsyncValidatablePayload(), cancellationToken).ConfigureAwait(false);
        checks++;
        await validatable.ValidateAsync("validatable", new DiagnosticsValidatablePayload(), cancellationToken).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<KyrolusMartenValidationException>(() => validatable.ValidateAsync("validatable", new object(), cancellationToken)).ConfigureAwait(false);
        checks++;

        var operationMap = new KyrolusMartenOperationMapValidation(
            new Dictionary<string, IKyrolusMartenValidation>(StringComparer.OrdinalIgnoreCase)
            {
                ["write"] = payloadNotNull
            },
            fallback: KyrolusMartenNoopValidation.Instance);
        await operationMap.ValidateAsync("write", new object(), cancellationToken).ConfigureAwait(false);
        checks++;
        await operationMap.ValidateAsync("read", null, cancellationToken).ConfigureAwait(false);
        checks++;

        var compositeStopOnFirst = new KyrolusMartenCompositeValidation([payloadNotNull], stopOnFirst: true);
        await compositeStopOnFirst.ValidateAsync("composite", new object(), cancellationToken).ConfigureAwait(false);
        checks++;

        var compositeAggregate = new KyrolusMartenCompositeValidation(
            [payloadNotNull, new KyrolusMartenDelegateValidation((_, _, _) => throw new KyrolusMartenValidationException("forced"))],
            stopOnFirst: false);
        await ExpectThrowsAsync<KyrolusMartenAggregateValidationException>(() => compositeAggregate.ValidateAsync("composite", null, cancellationToken)).ConfigureAwait(false);
        checks++;

        return checks;
    }

    private static async Task<int> RunTracingScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        await KyrolusMartenNoopTracing.Instance.RecordAsync("noop", null, TimeSpan.Zero, null, cancellationToken).ConfigureAwait(false);
        checks++;

        var delegateScopeDisposed = false;
        var delegateRecorded = 0;
        var delegateDisposed = false;
        var delegateTracing = new KyrolusMartenDelegateTracing(
            start: (_, _) => new DisposableScope(() => delegateScopeDisposed = true),
            record: (_, _, _, _, _) =>
            {
                delegateRecorded++;
                return Task.CompletedTask;
            },
            dispose: () =>
            {
                delegateDisposed = true;
                return ValueTask.CompletedTask;
            });

        using (delegateTracing.StartScope("delegate", new { Value = 1 }))
        {
        }
        await delegateTracing.RecordAsync("delegate", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await delegateTracing.DisposeAsync().ConfigureAwait(false);
        if (delegateScopeDisposed && delegateRecorded > 0 && delegateDisposed)
        {
            checks++;
        }

        var activityTracing = new KyrolusMartenActivityTracing(
            "KyrolusSous.Marten.Diagnostics",
            payload => [new KeyValuePair<string, object?>("payload.exists", payload is not null)]);
        using (activityTracing.StartScope("activity-ok", new { Value = 1 }))
        {
            await activityTracing.RecordAsync("activity-ok", null, TimeSpan.FromMilliseconds(2), null, cancellationToken).ConfigureAwait(false);
        }
        using (activityTracing.StartScope("activity-error", null))
        {
            await activityTracing.RecordAsync("activity-error", null, TimeSpan.FromMilliseconds(3), new InvalidOperationException("trace-error"), cancellationToken).ConfigureAwait(false);
        }
        await activityTracing.DisposeAsync().ConfigureAwait(false);
        checks++;

        var debugTracing = new KyrolusMartenDebugTracing();
        using (debugTracing.StartScope("debug", null))
        {
        }
        await debugTracing.RecordAsync("debug", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        checks++;

        var inMemoryTracing = new KyrolusMartenInMemoryTracing();
        await inMemoryTracing.RecordAsync("in-memory", new { Value = 1 }, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        if (inMemoryTracing.Snapshot().Count == 1)
        {
            checks++;
        }
        inMemoryTracing.Reset();
        if (inMemoryTracing.Snapshot().Count == 0)
        {
            checks++;
        }

        var filteredTracing = new KyrolusMartenOperationFilterTracing(
            operation => operation.StartsWith("allow", StringComparison.Ordinal),
            inMemoryTracing);
        using (filteredTracing.StartScope("allow-trace", null))
        {
        }
        await filteredTracing.RecordAsync("allow-trace", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await filteredTracing.RecordAsync("deny-trace", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        checks++;

        var errorOnlyTracing = new KyrolusMartenErrorOnlyTracing(inMemoryTracing);
        await errorOnlyTracing.RecordAsync("error-only-ok", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await errorOnlyTracing.RecordAsync("error-only-fail", null, TimeSpan.FromMilliseconds(1), new InvalidOperationException("error"), cancellationToken).ConfigureAwait(false);
        checks++;

        var thresholdTracing = new KyrolusMartenThresholdTracing(TimeSpan.FromMilliseconds(5), inMemoryTracing);
        await thresholdTracing.RecordAsync("threshold-low", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await thresholdTracing.RecordAsync("threshold-high", null, TimeSpan.FromMilliseconds(10), null, cancellationToken).ConfigureAwait(false);
        checks++;

        var samplingAlways = new KyrolusMartenSamplingTracing(1d, inMemoryTracing, new FixedRandom(0.0));
        using (samplingAlways.StartScope("sampling-always", null))
        {
            await samplingAlways.RecordAsync("sampling-always", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        }
        checks++;

        var samplingNever = new KyrolusMartenSamplingTracing(0d, inMemoryTracing, new FixedRandom(1.0));
        using (samplingNever.StartScope("sampling-never", null))
        {
            await samplingNever.RecordAsync("sampling-never", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        }
        checks++;

        var compositeTracing = new KyrolusMartenCompositeTracing([inMemoryTracing, KyrolusMartenNoopTracing.Instance]);
        using (compositeTracing.StartScope("composite-tracing", null))
        {
        }
        await compositeTracing.RecordAsync("composite-tracing", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await compositeTracing.DisposeAsync().ConfigureAwait(false);
        checks++;

        return checks;
    }

    private static async Task<int> RunObserverScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        await KyrolusMartenNoopObserver.Instance.OnBeforeAsync("noop", null, cancellationToken).ConfigureAwait(false);
        await KyrolusMartenNoopObserver.Instance.OnAfterAsync("noop", null, TimeSpan.Zero, null, cancellationToken).ConfigureAwait(false);
        checks++;

        var delegateBefore = 0;
        var delegateAfter = 0;
        var delegateObserver = new KyrolusMartenDelegateObserver(
            onBefore: (_, _, _) =>
            {
                delegateBefore++;
                return Task.CompletedTask;
            },
            onAfter: (_, _, _, _, _) =>
            {
                delegateAfter++;
                return Task.CompletedTask;
            });
        await delegateObserver.OnBeforeAsync("delegate", null, cancellationToken).ConfigureAwait(false);
        await delegateObserver.OnAfterAsync("delegate", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        if (delegateBefore == 1 && delegateAfter == 1)
        {
            checks++;
        }

        var debugObserver = new KyrolusMartenDebugObserver();
        await debugObserver.OnBeforeAsync("debug", null, cancellationToken).ConfigureAwait(false);
        await debugObserver.OnAfterAsync("debug", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        checks++;

        var errorCalls = 0;
        var errorOnly = new KyrolusMartenErrorOnlyObserver((_, _, _, _, _) =>
        {
            errorCalls++;
            return Task.CompletedTask;
        });
        await errorOnly.OnAfterAsync("error-only", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await errorOnly.OnAfterAsync("error-only", null, TimeSpan.FromMilliseconds(1), new InvalidOperationException("error"), cancellationToken).ConfigureAwait(false);
        if (errorCalls == 1)
        {
            checks++;
        }

        var slowCalls = 0;
        var slowObserver = new KyrolusMartenSlowOperationObserver(
            TimeSpan.FromMilliseconds(5),
            (_, _, _, _) =>
            {
                slowCalls++;
                return Task.CompletedTask;
            });
        await slowObserver.OnAfterAsync("slow", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await slowObserver.OnAfterAsync("slow", null, TimeSpan.FromMilliseconds(7), null, cancellationToken).ConfigureAwait(false);
        if (slowCalls == 1)
        {
            checks++;
        }

        var filteredInner = new KyrolusMartenCountingObserver(countOnBefore: true, countOnAfter: true);
        var filteredObserver = new KyrolusMartenOperationFilterObserver(
            operation => operation.StartsWith("allow", StringComparison.Ordinal),
            filteredInner);
        await filteredObserver.OnBeforeAsync("allow-before", null, cancellationToken).ConfigureAwait(false);
        await filteredObserver.OnAfterAsync("allow-after", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await filteredObserver.OnAfterAsync("deny-after", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        if (filteredInner.Snapshot().Count == 2)
        {
            checks++;
        }

        var compositeObserver = new KyrolusMartenCompositeObserver([delegateObserver, KyrolusMartenNoopObserver.Instance]);
        await compositeObserver.OnBeforeAsync("composite-before", null, cancellationToken).ConfigureAwait(false);
        await compositeObserver.OnAfterAsync("composite-after", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        checks++;

        var countingObserver = new KyrolusMartenCountingObserver(countOnBefore: true, countOnAfter: true, countFailuresOnly: true);
        await countingObserver.OnBeforeAsync("count", null, cancellationToken).ConfigureAwait(false);
        await countingObserver.OnAfterAsync("count", null, TimeSpan.FromMilliseconds(1), null, cancellationToken).ConfigureAwait(false);
        await countingObserver.OnAfterAsync("count", null, TimeSpan.FromMilliseconds(1), new InvalidOperationException("count"), cancellationToken).ConfigureAwait(false);
        if (countingObserver.Snapshot().TryGetValue("count", out var count) && count == 2)
        {
            checks++;
        }
        countingObserver.Reset();
        if (countingObserver.Snapshot().Count == 0)
        {
            checks++;
        }

        return checks;
    }

    private static async Task<int> RunResilienceScenariosAsync(CancellationToken cancellationToken)
    {
        var checks = 0;

        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenRetryResiliencePolicy(-1, TimeSpan.Zero));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenRetryResiliencePolicy(1, (Func<int, TimeSpan>)null!));
        checks++;
        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenTimeoutResiliencePolicy(TimeSpan.Zero));
        checks++;
        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenCircuitBreakerResiliencePolicy(0, TimeSpan.FromMilliseconds(1)));
        checks++;
        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenCircuitBreakerResiliencePolicy(1, TimeSpan.Zero));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenCompositeResiliencePolicy(null!));
        checks++;

        var noopResult = await KyrolusMartenNoopResiliencePolicy.Instance.ExecuteAsync("noop", () => Task.FromResult(1), cancellationToken).ConfigureAwait(false);
        if (noopResult == 1)
        {
            checks++;
        }
        await KyrolusMartenNoopResiliencePolicy.Instance.ExecuteAsync("noop-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        checks++;

        var delegatePolicy = new KyrolusMartenDelegateResiliencePolicy();
        var delegateResult = await delegatePolicy.ExecuteAsync("delegate", () => Task.FromResult(2), cancellationToken).ConfigureAwait(false);
        if (delegateResult == 2)
        {
            checks++;
        }
        await delegatePolicy.ExecuteAsync("delegate-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        checks++;

        var customDelegateGenericCalls = 0;
        var customDelegateVoidCalls = 0;
        var customDelegatePolicy = new KyrolusMartenDelegateResiliencePolicy(
            execute: async (operation, action, token) =>
            {
                if (operation == "delegate-custom-void" && token == cancellationToken)
                {
                    customDelegateVoidCalls++;
                }

                await action().ConfigureAwait(false);
            },
            executeT: async (operation, action, token) =>
            {
                if (operation == "delegate-custom-generic" && token == cancellationToken)
                {
                    customDelegateGenericCalls++;
                }

                return await action().ConfigureAwait(false);
            });
        var customDelegateResult = await customDelegatePolicy.ExecuteAsync("delegate-custom-generic", () => Task.FromResult(5), cancellationToken).ConfigureAwait(false);
        await customDelegatePolicy.ExecuteAsync("delegate-custom-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        if (customDelegateResult == 5 && customDelegateGenericCalls == 1 && customDelegateVoidCalls == 1)
        {
            checks++;
        }

        var retryAttempts = 0;
        var retryPolicy = new KyrolusMartenRetryResiliencePolicy(
            maxRetries: 2,
            delayFactory: _ => TimeSpan.Zero,
            shouldRetry: ex => ex is InvalidOperationException);

        var retryResult = await retryPolicy.ExecuteAsync("retry", () =>
        {
            retryAttempts++;
            if (retryAttempts < 3)
            {
                throw new InvalidOperationException("retry");
            }

            return Task.FromResult(7);
        }, cancellationToken).ConfigureAwait(false);
        if (retryResult == 7 && retryAttempts == 3)
        {
            checks++;
        }

        var retryVoidAttempts = 0;
        await retryPolicy.ExecuteAsync("retry-void", () =>
        {
            retryVoidAttempts++;
            if (retryVoidAttempts < 2)
            {
                throw new InvalidOperationException("retry-void");
            }

            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
        if (retryVoidAttempts == 2)
        {
            checks++;
        }

        var fixedDelayAttempts = 0;
        var fixedDelayPolicy = new KyrolusMartenRetryResiliencePolicy(
            maxRetries: 1,
            delay: TimeSpan.Zero,
            shouldRetry: ex => ex is InvalidOperationException,
            operationFilter: operation => operation == "retry-fixed");
        var fixedDelayResult = await fixedDelayPolicy.ExecuteAsync("retry-fixed", () =>
        {
            fixedDelayAttempts++;
            if (fixedDelayAttempts == 1)
            {
                throw new InvalidOperationException("retry-fixed");
            }

            return Task.FromResult(13);
        }, cancellationToken).ConfigureAwait(false);
        if (fixedDelayResult == 13 && fixedDelayAttempts == 2)
        {
            checks++;
        }

        await ExpectThrowsAsync<ArgumentException>(async () =>
        {
            await retryPolicy.ExecuteAsync("retry-no-match", () => throw new ArgumentException("no-retry"), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        checks++;

        var bypassAttempts = 0;
        var filteredRetry = new KyrolusMartenRetryResiliencePolicy(
            maxRetries: 2,
            delayFactory: _ => TimeSpan.Zero,
            operationFilter: operation => operation == "allowed");
        var bypassResult = await filteredRetry.ExecuteAsync("blocked", () =>
        {
            bypassAttempts++;
            return Task.FromResult(3);
        }, cancellationToken).ConfigureAwait(false);
        if (bypassResult == 3 && bypassAttempts == 1)
        {
            checks++;
        }

        var timeoutPolicy = new KyrolusMartenTimeoutResiliencePolicy(TimeSpan.FromMilliseconds(25));
        await timeoutPolicy.ExecuteAsync("timeout-fast", async () =>
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        checks++;
        var timeoutGenericFast = await timeoutPolicy.ExecuteAsync("timeout-fast-generic", async () =>
        {
            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
            return 4;
        }, cancellationToken).ConfigureAwait(false);
        if (timeoutGenericFast == 4)
        {
            checks++;
        }
        await ExpectThrowsAsync<TimeoutException>(async () =>
        {
            await timeoutPolicy.ExecuteAsync("timeout-slow", async () =>
            {
                await Task.Delay(75, cancellationToken).ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        checks++;
        await ExpectThrowsAsync<TimeoutException>(async () =>
        {
            await timeoutPolicy.ExecuteAsync("timeout-slow-generic", async () =>
            {
                await Task.Delay(75, cancellationToken).ConfigureAwait(false);
                return 8;
            }, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        checks++;

        var timeoutBypass = new KyrolusMartenTimeoutResiliencePolicy(
            TimeSpan.FromMilliseconds(1),
            operationFilter: operation => operation == "enforced");
        await timeoutBypass.ExecuteAsync("bypass", async () =>
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
        checks++;
        var timeoutBypassGeneric = await timeoutBypass.ExecuteAsync("bypass-generic", async () =>
        {
            await Task.Delay(5, cancellationToken).ConfigureAwait(false);
            return 6;
        }, cancellationToken).ConfigureAwait(false);
        if (timeoutBypassGeneric == 6)
        {
            checks++;
        }

        var circuitBreaker = new KyrolusMartenCircuitBreakerResiliencePolicy(
            failureThreshold: 2,
            breakDuration: TimeSpan.FromMilliseconds(150));
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitBreaker.ExecuteAsync("circuit", () => throw new InvalidOperationException("one"), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitBreaker.ExecuteAsync("circuit", () => throw new InvalidOperationException("two"), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitBreaker.ExecuteAsync("circuit", () => Task.FromResult(1), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        checks++;
        await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        var circuitRecovered = await circuitBreaker.ExecuteAsync("circuit", () => Task.FromResult(9), cancellationToken).ConfigureAwait(false);
        if (circuitRecovered == 9)
        {
            checks++;
        }

        var circuitVoid = new KyrolusMartenCircuitBreakerResiliencePolicy(
            failureThreshold: 2,
            breakDuration: TimeSpan.FromMilliseconds(150));
        var circuitVoidAttempts = 0;
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitVoid.ExecuteAsync("circuit-void", () =>
            {
                circuitVoidAttempts++;
                throw new InvalidOperationException("void-one");
            }, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitVoid.ExecuteAsync("circuit-void", () =>
            {
                circuitVoidAttempts++;
                throw new InvalidOperationException("void-two");
            }, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await ExpectThrowsAsync<InvalidOperationException>(async () =>
        {
            await circuitVoid.ExecuteAsync("circuit-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        await Task.Delay(180, cancellationToken).ConfigureAwait(false);
        await circuitVoid.ExecuteAsync("circuit-void", () =>
        {
            circuitVoidAttempts++;
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
        if (circuitVoidAttempts == 3)
        {
            checks++;
        }

        var filteredCircuitAttempts = 0;
        var filteredCircuit = new KyrolusMartenCircuitBreakerResiliencePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50),
            operationFilter: operation => operation == "circuit-filtered");
        await filteredCircuit.ExecuteAsync("circuit-bypass", () =>
        {
            filteredCircuitAttempts++;
            return Task.CompletedTask;
        }, cancellationToken).ConfigureAwait(false);
        if (filteredCircuitAttempts == 1)
        {
            checks++;
        }

        var nonTrippingCircuit = new KyrolusMartenCircuitBreakerResiliencePolicy(
            failureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50),
            shouldTrip: ex => ex is InvalidOperationException);
        await ExpectThrowsAsync<ArgumentException>(async () =>
        {
            await nonTrippingCircuit.ExecuteAsync("circuit-no-trip", () => throw new ArgumentException("skip-trip"), cancellationToken).ConfigureAwait(false);
        }).ConfigureAwait(false);
        var nonTrippingResult = await nonTrippingCircuit.ExecuteAsync("circuit-no-trip", () => Task.FromResult(10), cancellationToken).ConfigureAwait(false);
        if (nonTrippingResult == 10)
        {
            checks++;
        }

        var compositePolicy = new KyrolusMartenCompositeResiliencePolicy([retryPolicy, timeoutPolicy]);
        var compositeResult = await compositePolicy.ExecuteAsync("composite", () => Task.FromResult(11), cancellationToken).ConfigureAwait(false);
        if (compositeResult == 11)
        {
            checks++;
        }
        await compositePolicy.ExecuteAsync("composite-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        checks++;

        var emptyComposite = new KyrolusMartenCompositeResiliencePolicy(Array.Empty<IKyrolusMartenResiliencePolicy>());
        var emptyCompositeResult = await emptyComposite.ExecuteAsync("empty-composite", () => Task.FromResult(12), cancellationToken).ConfigureAwait(false);
        await emptyComposite.ExecuteAsync("empty-composite-void", () => Task.CompletedTask, cancellationToken).ConfigureAwait(false);
        if (emptyCompositeResult == 12)
        {
            checks++;
        }

        return checks;
    }

    private static async Task<int> RunSpecificationScenariosAsync(
        IDocumentSession session,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var checks = 0;

        var tenantBaseQuery = (IMartenQueryable<MenuItem>)session.Query<MenuItem>()
            .Where(x => x.TenantId == tenantId);
        var tenantDiagQuery = (IMartenQueryable<MenuItem>)session.Query<MenuItem>()
            .Where(x => x.TenantId == tenantId && x.Category == "DiagAbstractions");

        var delegateSpec = new KyrolusMartenDelegateSpecification<MenuItem>(q =>
            (IMartenQueryable<MenuItem>)q.Where(x => x.Price > 0));
        var delegateItems = await delegateSpec.Apply(tenantDiagQuery).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (delegateItems.Count > 0)
        {
            checks++;
        }

        var filterSpec = new KyrolusMartenFilterSpecification<MenuItem>(x => x.Category == "DiagAbstractions");
        var filtered = await filterSpec.Apply(tenantBaseQuery).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (filtered.Count > 0 && filtered.All(x => x.Category == "DiagAbstractions"))
        {
            checks++;
        }

        var orderSpec = new KyrolusMartenOrderSpecification<MenuItem>(q => q.OrderByDescending(x => x.Price));
        var ordered = await orderSpec.Apply(tenantDiagQuery).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (ordered.Count <= 1 || ordered[0].Price >= ordered[^1].Price)
        {
            checks++;
        }

        var paginationSpec = new KyrolusMartenPaginationSpecification<MenuItem>(skip: 0, take: 1);
        var paged = await paginationSpec.Apply(tenantDiagQuery).ToListAsync(cancellationToken).ConfigureAwait(false);
        if (paged.Count == 1)
        {
            checks++;
        }

        var includeCalled = false;
        var includeSpec = new KyrolusMartenIncludeSpecification<MenuItem>(_ => includeCalled = true);
        var includeResult = includeSpec.Apply(tenantDiagQuery);
        if (includeCalled && ReferenceEquals(includeResult, tenantDiagQuery))
        {
            checks++;
        }

        var compositeSpec = new KyrolusMartenCompositeSpecification<MenuItem>([
            new KyrolusMartenFilterSpecification<MenuItem>(x => x.TenantId == tenantId),
            new KyrolusMartenOrderSpecification<MenuItem>(q => q.OrderBy(x => x.Name)),
            new KyrolusMartenPaginationSpecification<MenuItem>(skip: 0, take: 1)
        ]);
        var composite = await compositeSpec.Apply((IMartenQueryable<MenuItem>)session.Query<MenuItem>())
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (composite.Count == 1 && composite[0].TenantId == tenantId)
        {
            checks++;
        }

        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenPaginationSpecification<MenuItem>(skip: -1, take: 1));
        checks++;
        ExpectThrows<ArgumentOutOfRangeException>(() => _ = new KyrolusMartenPaginationSpecification<MenuItem>(skip: 0, take: 0));
        checks++;

        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenDelegateSpecification<MenuItem>(null!));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenFilterSpecification<MenuItem>(null!));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenOrderSpecification<MenuItem>(null!));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenIncludeSpecification<MenuItem>(null!));
        checks++;
        ExpectThrows<ArgumentNullException>(() => _ = new KyrolusMartenCompositeSpecification<MenuItem>(null!));
        checks++;

        return checks;
    }

    private static Task<int> RunQueryPrimitiveScenariosAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var checks = 0;

        if (QueryRequest.TryParse(null, provider: null, out var emptyRequest) &&
            emptyRequest.Includes is null &&
            emptyRequest.Filters is null &&
            emptyRequest.OrderBy is null)
        {
            checks++;
        }

        var encodedQueryRequest = Uri.EscapeDataString("""
            {
              "includes": ["Payment"],
              "fields": ["Id", "Name"],
              "orderBy": [{ "property": "Price", "desc": true }],
              "filters": [{ "property": "Category", "operator": "eq", "value": "DiagAbstractions" }],
              "asNoTracking": true,
              "useSplitQuery": false,
              "includeDeleted": false
            }
            """);

        if (QueryRequest.TryParse(encodedQueryRequest, provider: null, out var parsedRequest) &&
            parsedRequest.Includes?.Length == 1 &&
            parsedRequest.OrderBy?.Length == 1 &&
            parsedRequest.Filters?.Length == 1 &&
            parsedRequest.AsNoTracking == true &&
            parsedRequest.UseSplitQuery == false)
        {
            checks++;
        }

        if (!QueryRequest.TryParse("{not-json", provider: null, out _))
        {
            checks++;
        }

        ExpectThrows<FormatException>(() => _ = QueryRequest.Parse("{not-json", provider: null));
        checks++;

        var includeGraph = new IncludeGraph<MenuItem>(x => x.Name, x => x.Category);
        if (includeGraph.Includes.Count == 2)
        {
            checks++;
        }

        var emptyIncludeGraph = new IncludeGraph<MenuItem>();
        if (emptyIncludeGraph.Includes.Count == 0)
        {
            checks++;
        }

        var parts = new QueryParts<MenuItem>(
            Filter: x => x.Price > 0,
            OrderBy: q => q.OrderBy(x => x.Name),
            Includes: [x => x.Name],
            AsNoTracking: true,
            UseSplitQuery: false,
            IncludeDeleted: false,
            IncludeGraph: includeGraph);
        if (parts.Filter is not null &&
            parts.OrderBy is not null &&
            parts.Includes.Length == 1 &&
            parts.IncludeGraph is not null &&
            parts.AsNoTracking == true)
        {
            checks++;
        }

        var queryRequest = new QueryRequest(IncludeGraph: includeGraph, AsNoTracking: true, IncludeDeleted: true);
        if (queryRequest.IncludeGraph is IncludeGraph<MenuItem> &&
            queryRequest.AsNoTracking == true &&
            queryRequest.IncludeDeleted == true)
        {
            checks++;
        }

        var context = new KyrolusMartenAuthorizationContext(Roles: null, Permissions: null);
        if (context.Roles.Count == 0 && context.Permissions.Count == 0)
        {
            checks++;
        }

        var includeProbe = new RuntimeQueryBuilderProbe
        {
            Nested = new RuntimeQueryBuilderNested("nested-name"),
            StatusFromText = RuntimeSeekProbeStatus.Active
        };
        var includeExpressions = KyrolusQueryExpressionBuilder<RuntimeQueryBuilderProbe>.ConvertIncludePropertiesToExpressions(
            [nameof(RuntimeQueryBuilderProbe.Nested) + "." + nameof(RuntimeQueryBuilderNested.Name), " ", nameof(RuntimeQueryBuilderProbe.StatusFromText)]);
        if (KyrolusQueryExpressionBuilder<RuntimeQueryBuilderProbe>.BuildIncludeExpression(" ") is null &&
            KyrolusQueryExpressionBuilder<RuntimeQueryBuilderProbe>.ConvertIncludePropertiesToExpressions(null) is null &&
            includeExpressions is { Length: 2 } &&
            (string?)includeExpressions[0].Compile().Invoke(includeProbe) == "nested-name" &&
            includeExpressions[1].Compile().Invoke(includeProbe) is RuntimeSeekProbeStatus.Active)
        {
            checks++;
        }

        ExpectThrows<ArgumentException>(
            () => _ = KyrolusQueryExpressionBuilder<RuntimeQueryBuilderProbe>.GetPrimaryKeyFromKeyValues(
                [1],
                [nameof(RuntimeQueryBuilderProbe.Sequence), nameof(RuntimeQueryBuilderProbe.OptionalSequence)]));
        checks++;

        var convertedGuid = Guid.NewGuid();
        var directGuid = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var happenedOn = DateTime.UtcNow.AddDays(-1);
        var duration = TimeSpan.FromMinutes(15);
        var convertedPredicate = KyrolusQueryExpressionBuilder<RuntimeQueryBuilderProbe>.GetPrimaryKeyFromKeyValues(
            [
                convertedGuid.ToString(),
                directGuid,
                occurredAt.ToString("O"),
                happenedOn.ToString("O"),
                duration.ToString(),
                "Active",
                1,
                "7",
                null
            ],
            [
                nameof(RuntimeQueryBuilderProbe.Id),
                nameof(RuntimeQueryBuilderProbe.DirectId),
                nameof(RuntimeQueryBuilderProbe.OccurredAt),
                nameof(RuntimeQueryBuilderProbe.HappenedOn),
                nameof(RuntimeQueryBuilderProbe.Duration),
                nameof(RuntimeQueryBuilderProbe.StatusFromText),
                nameof(RuntimeQueryBuilderProbe.StatusFromNumber),
                nameof(RuntimeQueryBuilderProbe.Sequence),
                nameof(RuntimeQueryBuilderProbe.OptionalSequence)
            ]).Compile();
        if (convertedPredicate(new RuntimeQueryBuilderProbe
            {
                Id = convertedGuid,
                DirectId = directGuid,
                OccurredAt = occurredAt,
                HappenedOn = happenedOn,
                HappenedDate = DateOnly.FromDateTime(happenedOn),
                HappenedTime = TimeOnly.FromDateTime(happenedOn),
                Duration = duration,
                StatusFromText = RuntimeSeekProbeStatus.Active,
                StatusFromNumber = RuntimeSeekProbeStatus.Active,
                NullableStatus = RuntimeSeekProbeStatus.Active,
                Sequence = 7,
                OptionalSequence = null
            }) &&
            !convertedPredicate(new RuntimeQueryBuilderProbe
            {
                Id = convertedGuid,
                DirectId = directGuid,
                OccurredAt = occurredAt,
                HappenedOn = happenedOn,
                HappenedDate = DateOnly.FromDateTime(happenedOn),
                HappenedTime = TimeOnly.FromDateTime(happenedOn),
                Duration = duration,
                StatusFromText = RuntimeSeekProbeStatus.New,
                StatusFromNumber = RuntimeSeekProbeStatus.Active,
                NullableStatus = RuntimeSeekProbeStatus.Active,
                Sequence = 7,
                OptionalSequence = null
            }))
        {
            checks++;
        }

        if (!RuntimeMartenFilterExpressionBuilderProbe.ProbeTryBuildMemberAccess(".", out _, out var emptyPropertyError) &&
            emptyPropertyError == "Property name is required.")
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeTryBuildFilterExpression(
                typeof(RuntimeQueryBuilderProbe),
                "NullableStatus in [Active]",
                caseInsensitive: false,
                out var nullableEnumExpression,
                out var nullableEnumError) &&
            nullableEnumExpression is Expression<Func<RuntimeQueryBuilderProbe, bool>> nullableEnumPredicate &&
            nullableEnumError is null)
        {
            var compiled = nullableEnumPredicate.Compile();
            if (compiled(new RuntimeQueryBuilderProbe { NullableStatus = RuntimeSeekProbeStatus.Active }) &&
                !compiled(new RuntimeQueryBuilderProbe { NullableStatus = RuntimeSeekProbeStatus.New }) &&
                !compiled(new RuntimeQueryBuilderProbe { NullableStatus = null }))
            {
                checks++;
            }
        }

        if (!RuntimeMartenFilterExpressionBuilderProbe.ProbeTryBuildFilterExpression(
                typeof(RuntimeQueryBuilderProbe),
                "StatusFromText in [invalid-enum]",
                caseInsensitive: false,
                out _,
                out var invalidEnumListError) &&
            invalidEnumListError?.Contains("RuntimeSeekProbeStatus", StringComparison.Ordinal) == true)
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeTrySplitBetween(
                "'left\\'value'..\"right\\\"value\"",
                out var escapedStart,
                out var escapedEnd) &&
            escapedStart == "left'value" &&
            escapedEnd == "right\"value")
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert(null, typeof(DateOnly), out var nullDateOnly) &&
            nullDateOnly is null &&
            !RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("bad-datetime", typeof(DateTime), out _) &&
            !RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("bad-dateonly", typeof(DateOnly), out _) &&
            !RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("bad-timeonly", typeof(TimeOnly), out _) &&
            !RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("bad-enum", typeof(RuntimeSeekProbeStatus), out _))
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("2024-03-02T10:11:12Z", typeof(DateTime), out var parsedDateTime) &&
            parsedDateTime is DateTime parsedDateTimeValue &&
            RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("2024-03-02", typeof(DateOnly), out var parsedDateOnly) &&
            parsedDateOnly is DateOnly parsedDateOnlyValue &&
            RuntimeMartenFilterExpressionBuilderProbe.ProbeTryConvert("10:11:12", typeof(TimeOnly), out var parsedTimeOnly) &&
            parsedTimeOnly is TimeOnly parsedTimeOnlyValue &&
            parsedDateTimeValue == DateTime.Parse("2024-03-02T10:11:12Z", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) &&
            parsedDateOnlyValue == new DateOnly(2024, 3, 2) &&
            parsedTimeOnlyValue == new TimeOnly(10, 11, 12))
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeTryBuildFilterExpression(
                typeof(RuntimeQueryBuilderProbe),
                "HappenedDate in ['2024-03-01','2024-03-02']",
                caseInsensitive: false,
                out var dateListExpression,
                out var dateListError) &&
            dateListExpression is Expression<Func<RuntimeQueryBuilderProbe, bool>> dateListPredicate &&
            dateListError is null &&
            dateListPredicate.Compile().Invoke(new RuntimeQueryBuilderProbe { HappenedDate = new DateOnly(2024, 3, 2) }) &&
            !dateListPredicate.Compile().Invoke(new RuntimeQueryBuilderProbe { HappenedDate = new DateOnly(2024, 3, 5) }))
        {
            checks++;
        }

        if (RuntimeMartenFilterExpressionBuilderProbe.ProbeThrowsUnsupportedComparison())
        {
            checks++;
        }

        return Task.FromResult(checks);
    }

    private static async Task<int> RunBestEffortAsync(Func<Task<int>> scenario)
    {
        try
        {
            return await scenario().ConfigureAwait(false);
        }
        catch
        {
            return 0;
        }
    }

}
