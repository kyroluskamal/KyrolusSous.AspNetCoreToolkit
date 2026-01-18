using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.EF.Query;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Authorization;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling;
using KyrolusSous.ExceptionHandling.Abstractions.Models;
using KyrolusSous.ExceptionHandling.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Validation.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class DefaultCommandQueryHandler<TResponse, TModel, TKey>(
    IKyrolusMapper mapper,
    IKyrolusMediatorSender mediator,
    IKyrolusApiConfig<TResponse> config,
    IServiceProvider serviceProvider)
    : ICommandQueryHandler<TResponse, TModel, TKey>,
      IKyrolusEfCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private const string ConcurrencyConflictMessage = "Concurrency conflict.";
    private const string IncludeDeletedPropertyName = "IncludeDeleted";
    private const string KeyValuesPropertyName = "KeyValues";
    private const string UseSplitQueryPropertyName = "UseSplitQuery";
    private readonly IKyrolusMapper mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private readonly IKyrolusMediatorSender mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    private readonly IKyrolusApiConfig<TResponse> config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IKyrolusEfApiConfig<TResponse>? efConfig = config as IKyrolusEfApiConfig<TResponse>;
    private readonly IKyrolusEfAuthorizationProvider<TResponse> authorizationProvider =
        serviceProvider.GetService<IKyrolusEfAuthorizationProvider<TResponse>>()
        ?? KyrolusNoopEfAuthorizationProvider<TResponse>.Instance;
    private readonly IKyrolusEndpointContext? endpointContext = serviceProvider.GetService<IKyrolusEndpointContext>();
    private readonly IKyrolusValidationEngine? validationEngine = serviceProvider.GetService<IKyrolusValidationEngine>();
    private readonly IKyrolusErrorResponseWriter? errorWriter = serviceProvider.GetService<IKyrolusErrorResponseWriter>();
    private readonly KyrolusHttpErrorContextFactory? errorContextFactory = serviceProvider.GetService<KyrolusHttpErrorContextFactory>();
    private readonly IHttpContextAccessor? httpContextAccessor = serviceProvider.GetService<IHttpContextAccessor>();

    private HttpContext? HttpContext => httpContextAccessor?.HttpContext;

    public async Task<IResult> HandleGetAllAsync(string? filter = null, string? includedProps = null, string? includeGraph = null, string? fields = null, bool? cacheable = null, bool? includeDeleted = null)
    {
        var requestedIncludes = SplitCsv(includedProps);
        var requestedFields = SplitCsv(fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.GetAll,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.GetAll, requestedIncludes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.GetAll, SplitCsv(includeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetAll, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryAll;
        ApplyCacheable(query, cacheable);
        DefaultCommandQueryHandler<TResponse, TModel, TKey>.ApplyGetAllQueryOptions(
            query,
            new GetAllQueryOptions(
                filterExpr,
                OrderBy: null,
                IncludeProperties: useStringIncludes ? includes : null,
                IncludeExpressions: includeExpressions,
                IncludeGraph: includeGraphValue,
                AsNoTracking: null,
                UseSplitQuery: null));

        var useProjection = TryBuildProjectionSelector(EndpointNames.GetAll, selectedFields, out var selector);
        if (query is GetAllQuery<TResponse> getAllQuery)
        {
            getAllQuery.IncludeDeleted = includeDeleted ?? false;
            getAllQuery.DeletedOnly = false;
            if (useProjection) getAllQuery.Selector = selector;
        }
        else
        {
            TrySetProperty(query, IncludeDeletedPropertyName, includeDeleted ?? false);
            TrySetProperty(query, "DeletedOnly", false);
            if (useProjection) TrySetProperty(query, "Selector", selector);
        }

        var result = await mediator.SendAsync(query, CancellationToken.None);
        return BuildSuccess(result ?? Array.Empty<TResponse>(), EndpointNames.GetAll, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleGetByIdAsync(TKey id, string? includedProps = null, string? includeGraph = null, string? fields = null, bool? cacheable = null, bool? includeDeleted = null)
    {
        var requestedIncludes = SplitCsv(includedProps);
        var requestedFields = SplitCsv(fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.GetById,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: id,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildIncludes(EndpointNames.GetById, requestedIncludes, authResult.AllowedIncludes, out var includes, out var errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.GetById, SplitCsv(includeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetById, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryRequireTenant(out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryById;
        ApplyCacheable(query, cacheable);
        ApplyGetByIdQueryOptions(query, id, useStringIncludes ? includes : null, includeExpressions, includeGraphValue, asNoTracking: null, useSplitQuery: null);
        if (includeDeleted == true)
        {
            TrySetProperty(query, IncludeDeletedPropertyName, true);
        }

        var result = await mediator.SendAsync(query, CancellationToken.None);
        if (result is null) return BuildNotFound();
        if (!TryEnsureTenantMatch(result, out errorResult)) return errorResult!;
        if (!TryEnsureRowAuthorization(result, authResult, out errorResult)) return errorResult!;
        if (TryBuildNotModifiedResult(result, out var notModifiedResult)) return notModifiedResult;
        TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.GetById, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleCreateAsync(TModel model, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Add,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var validationResult = await ValidateModelAsync(model, CancellationToken.None).ConfigureAwait(false);
        if (validationResult is not null) return validationResult;
        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(model);
        IResult? errorResult;
        if (!TryApplyContextValues(entity, out errorResult)) return errorResult!;
        var command = config.AddCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.Add, StatusCodes.Status201Created);
    }

    public async Task<IResult> HandleCreateRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.AddRange,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var validationResult = await ValidateModelRangeAsync(model, CancellationToken.None).ConfigureAwait(false);
        if (validationResult is not null) return validationResult;
        var entities = (IEnumerable<TResponse>)mapper.MapModelToEntity<TModel, TResponse>(model);
        IResult? errorResult;
        if (!TryApplyContextValues(entities, out errorResult)) return errorResult!;
        var command = config.AddRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.AddRange, StatusCodes.Status201Created);
    }

    public async Task<IResult> HandleUpdateAsync(TKey id, TModel model, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Update,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: id,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var validationResult = await ValidateModelAsync(model, CancellationToken.None).ConfigureAwait(false);
        if (validationResult is not null) return validationResult;
        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(model);
        IResult? errorResult;
        if (!TrySetEntityId(entity, id, out errorResult)) return errorResult!;
        if (!TryApplyContextValues(entity, out errorResult)) return errorResult!;
        if (!TryApplyIfMatch(entity, out errorResult)) return errorResult!;
        var accessResult = await TryEnsureAccessAsync(id, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        var command = config.UpdateCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        TResponse result;
        try
        {
            result = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.Update, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleUpdateRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.UpdateRange,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var validationResult = await ValidateModelRangeAsync(model, CancellationToken.None).ConfigureAwait(false);
        if (validationResult is not null) return validationResult;
        var entities = (IEnumerable<TResponse>)mapper.MapModelToEntity<TModel, TResponse>(model);
        IResult? errorResult;
        if (!TryApplyContextValues(entities, out errorResult)) return errorResult!;
        var command = config.UpdateRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        IEnumerable<TResponse> result;
        try
        {
            result = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        return BuildSuccess(result, EndpointNames.UpdateRange, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveAsync(TKey id, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Delete,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: id,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var keyValues = BuildKeyValues(id);
        IKyrolusCommandBase command = config.RemoveCommand;
        if (efConfig?.UseSoftDeleteForDelete == true)
        {
            command = new SoftDeleteByIdCommand<TResponse, TKey>(keyValues, cacheable ?? false);
        }

        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        var accessResult = await TryEnsureAccessAsync(id, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        try
        {
            await SendCommandAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        return BuildSuccess(true, EndpointNames.Delete, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.DeleteRange,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var entities = (IEnumerable<TResponse>)mapper.MapModelToEntity<TModel, TResponse>(model);
        var command = config.RemoveRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(true, EndpointNames.DeleteRange, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandlePatchAsync(TKey id, Dictionary<string, object> updates, bool? cacheable = null)
    {
        ArgumentNullException.ThrowIfNull(updates);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Patch,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: updates?.Keys.ToArray(),
            resourceId: id,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out var errorResult)) return errorResult!;
        if (!TryRejectContextUpdates(updates!, out errorResult)) return errorResult!;
        if (!TryApplyPatchPermissions(updates!, authResult.AllowedPatchProperties, out var filteredUpdates, out errorResult)) return errorResult!;
        if (filteredUpdates.Count == 0)
        {
            return BuildBadRequest("No patch fields are allowed.");
        }
        var ifMatchResult = await TryEnsureIfMatchAsync(id, cacheable).ConfigureAwait(false);
        if (!ifMatchResult.Success) return ifMatchResult.Error!;
        var accessResult = await TryEnsureAccessAsync(id, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;
        var keyValues = BuildKeyValues(id);
        var command = config.PatchCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        TrySetProperty(command, "Updates", filteredUpdates);

        TResponse? result;
        try
        {
            result = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        if (result is not null) TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.Patch, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleGetByKeysAsync(string[]? keys, string? includedProps = null, string? includeGraph = null, string? fields = null, bool? cacheable = null, bool? includeDeleted = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        var requestedIncludes = SplitCsv(includedProps);
        var requestedFields = SplitCsv(fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.GetById,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: keyValues,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildIncludes(EndpointNames.GetById, requestedIncludes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.GetById, SplitCsv(includeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetById, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryRequireTenant(out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = efConfig?.QueryByKeyValues ?? new GetByKeyValuesQuery<TResponse, TKey>(keyValues, cacheable ?? false);

        ApplyCacheable(query, cacheable);
        ApplyGetByKeyValuesQueryOptions(query, keyValues, useStringIncludes ? includes : null, includeExpressions, includeGraphValue, asNoTracking: null, useSplitQuery: null);
        if (includeDeleted == true)
        {
            TrySetProperty(query, IncludeDeletedPropertyName, true);
        }

        var result = await mediator.SendAsync(query, CancellationToken.None);
        if (result is null) return BuildNotFound();
        if (!TryEnsureTenantMatch(result, out errorResult)) return errorResult!;
        if (!TryEnsureRowAuthorization(result, authResult, out errorResult)) return errorResult!;
        if (TryBuildNotModifiedResult(result, out var notModifiedResult)) return notModifiedResult;
        TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.GetById, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleUpdateByKeysAsync(string[]? keys, TModel model, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Update,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: keyValues,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var validationResult = await ValidateModelAsync(model, CancellationToken.None).ConfigureAwait(false);
        if (validationResult is not null) return validationResult;
        if (!TryRequireTenant(out errorResult)) return errorResult!;

        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(model);
        if (!TrySetCompositeKey(entity, keyValues, out errorResult)) return errorResult!;
        if (!TryApplyContextValues(entity, out errorResult)) return errorResult!;
        if (!TryApplyIfMatch(entity, out errorResult)) return errorResult!;
        var accessResult = await TryEnsureAccessAsync(keyValues, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        var command = config.UpdateCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        TResponse result;
        try
        {
            result = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.Update, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveByKeysAsync(string[]? keys, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Delete,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: keyValues,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        IKyrolusCommandBase command = config.RemoveCommand;
        if (efConfig?.UseSoftDeleteForDelete == true)
        {
            command = new SoftDeleteByIdCommand<TResponse, TKey>(keyValues, cacheable ?? false);
        }

        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        var accessResult = await TryEnsureAccessAsync(keyValues, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        try
        {
            await SendCommandAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        return BuildSuccess(true, EndpointNames.Delete, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandlePatchByKeysAsync(string[]? keys, Dictionary<string, object> updates, bool? cacheable = null)
    {
        ArgumentNullException.ThrowIfNull(updates);
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Patch,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: updates?.Keys.ToArray(),
            resourceId: null,
            keyValues: keyValues,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out errorResult)) return errorResult!;
        if (!TryRejectContextUpdates(updates!, out errorResult)) return errorResult!;
        if (!TryApplyPatchPermissions(updates!, authResult.AllowedPatchProperties, out var filteredUpdates, out errorResult)) return errorResult!;
        if (filteredUpdates.Count == 0)
        {
            return BuildBadRequest("No patch fields are allowed.");
        }
        var ifMatchResult = await TryEnsureIfMatchAsync(keyValues, cacheable).ConfigureAwait(false);
        if (!ifMatchResult.Success) return ifMatchResult.Error!;
        var accessResult = await TryEnsureAccessAsync(keyValues, includeDeleted: false, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        var command = config.PatchCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        TrySetProperty(command, "Updates", filteredUpdates);

        TResponse? result;
        try
        {
            result = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        if (result is not null) TrySetEtagHeader(result);
        return BuildSuccess(result, EndpointNames.Patch, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleGetDeletedAsync(string? filter = null, string? includedProps = null, string? includeGraph = null, string? fields = null, bool? cacheable = null)
    {
        var requestedIncludes = SplitCsv(includedProps);
        var requestedFields = SplitCsv(fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.GetDeleted,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.GetDeleted, requestedIncludes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.GetDeleted, SplitCsv(includeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetDeleted, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryAll;
        ApplyCacheable(query, cacheable);
        DefaultCommandQueryHandler<TResponse, TModel, TKey>.ApplyGetAllQueryOptions(
            query,
            new GetAllQueryOptions(
                filterExpr,
                OrderBy: null,
                IncludeProperties: useStringIncludes ? includes : null,
                IncludeExpressions: includeExpressions,
                IncludeGraph: includeGraphValue,
                AsNoTracking: null,
                UseSplitQuery: null));

        var useProjection = TryBuildProjectionSelector(EndpointNames.GetDeleted, selectedFields, out var selector);
        if (query is GetAllQuery<TResponse> getAllQuery)
        {
            getAllQuery.IncludeDeleted = true;
            getAllQuery.DeletedOnly = true;
            if (useProjection) getAllQuery.Selector = selector;
        }
        else
        {
            TrySetProperty(query, IncludeDeletedPropertyName, true);
            TrySetProperty(query, "DeletedOnly", true);
            if (useProjection) TrySetProperty(query, "Selector", selector);
        }

        var result = await mediator.SendAsync(query, CancellationToken.None);
        return BuildSuccess(result ?? Array.Empty<TResponse>(), EndpointNames.GetDeleted, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleRestoreAsync(TKey id, bool? cacheable = null)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Restore,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: id,
            keyValues: null,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out var errorResult)) return errorResult!;
        var accessResult = await TryEnsureAccessAsync(id, includeDeleted: true, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        var keyValues = BuildKeyValues(id);
        var command = efConfig?.RestoreCommand ?? new RestoreByIdCommand<TResponse, TKey>(keyValues, cacheable ?? false);
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);

        bool restored;
        try
        {
            restored = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        return BuildSuccess(restored, EndpointNames.Restore, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRestoreByKeysAsync(string[]? keys, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Restore,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: keyValues,
            CancellationToken.None).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out errorResult)) return errorResult!;
        var accessResult = await TryEnsureAccessAsync(keyValues, includeDeleted: true, cacheable, authResult).ConfigureAwait(false);
        if (!accessResult.Success) return accessResult.Error!;

        var command = efConfig?.RestoreCommand ?? new RestoreByIdCommand<TResponse, TKey>(keyValues, cacheable ?? false);
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, KeyValuesPropertyName, keyValues);

        bool restored;
        try
        {
            restored = await mediator.SendAsync(command, CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return BuildConflict(ConcurrencyConflictMessage);
        }
        return BuildSuccess(restored, EndpointNames.Restore, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleQueryAsync(QueryRequest? request, bool? cacheable = null, bool? includeDeleted = null, CancellationToken cancellationToken = default)
    {
        request ??= new QueryRequest();
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Query,
            request.Fields,
            request.Includes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(request.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(request.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.Query, request.Includes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.Query, request.IncludeGraph, authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.Query, request.Fields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryByProperty;
        ApplyCacheable(query, cacheable);
        DefaultCommandQueryHandler<TResponse, TModel, TKey>.ApplyGetAllQueryOptions(
            query,
            new GetAllQueryOptions(
                filterExpr,
                orderExpr,
                IncludeProperties: useStringIncludes ? includes : null,
                IncludeExpressions: includeExpressions,
                IncludeGraph: includeGraphValue,
                AsNoTracking: request.AsNoTracking,
                UseSplitQuery: request.UseSplitQuery));

        var useProjection = TryBuildProjectionSelector(EndpointNames.Query, selectedFields, out var selector);
        if (query is GetAllQuery<TResponse> getAllQuery)
        {
            getAllQuery.IncludeDeleted = includeDeleted ?? false;
            getAllQuery.DeletedOnly = false;
            if (useProjection) getAllQuery.Selector = selector;
        }
        else
        {
            TrySetProperty(query, IncludeDeletedPropertyName, includeDeleted ?? false);
            TrySetProperty(query, "DeletedOnly", false);
            if (useProjection) TrySetProperty(query, "Selector", selector);
        }

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result ?? [], EndpointNames.Query, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleGetAllPagedAsync(KyrolusEfQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var requestedIncludes = SplitCsv(parameters.Includes);
        var requestedFields = SplitCsv(parameters.Fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Paged,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(parameters.Filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(parameters.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.Paged, requestedIncludes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.Paged, SplitCsv(parameters.IncludeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.Paged, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var (pageNumber, pageSize) = NormalizePaging(parameters.PageNumber, parameters.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var includeDeleted = parameters.IncludeDeleted ?? false;
        var useProjection = TryBuildProjectionSelector(EndpointNames.Paged, selectedFields, out var selector);
        if (useStringIncludes || includeDeleted)
        {
            var options = new GetAllPagedOptions(
                filterExpr,
                orderExpr,
                includes,
                includeGraphValue,
                parameters.AsNoTracking,
                parameters.UseSplitQuery,
                pageNumber,
                pageSize,
                parameters.Cacheable,
                includeDeleted,
                useProjection ? selector : null);
            var paged = await BuildPagedFromGetAll(options, cancellationToken);
            return BuildSuccess(paged, EndpointNames.Paged, StatusCodes.Status200OK, useProjection ? null : selectedFields);
        }

        var query = new GetPagedQuery<TResponse, TKey>(pageNumber, pageSize, parameters.Cacheable ?? false)
        {
            Filter = filterExpr,
            OrderBy = orderExpr,
            IncludeExpressions = includeExpressions,
            IncludeGraph = includeGraphValue,
            AsNoTracking = parameters.AsNoTracking,
            UseSplitQuery = parameters.UseSplitQuery
        };
        if (useProjection)
        {
            query.Selector = selector;
        }

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.Paged, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleQueryPagedAsync(KyrolusEfPagedQueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryRequest = request.Request ?? new QueryRequest();
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.QueryPaged,
            queryRequest.Fields,
            queryRequest.Includes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(queryRequest.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.QueryPaged, queryRequest.Includes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.QueryPaged, queryRequest.IncludeGraph, authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.QueryPaged, queryRequest.Fields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var (pageNumber, pageSize) = NormalizePaging(request.PageNumber, request.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var includeDeleted = request.IncludeDeleted ?? false;
        var useProjection = TryBuildProjectionSelector(EndpointNames.QueryPaged, selectedFields, out var selector);
        if (useStringIncludes || includeDeleted)
        {
            var options = new GetAllPagedOptions(
                filterExpr,
                orderExpr,
                includes,
                includeGraphValue,
                queryRequest.AsNoTracking,
                queryRequest.UseSplitQuery,
                pageNumber,
                pageSize,
                request.Cacheable,
                includeDeleted,
                useProjection ? selector : null);
            var paged = await BuildPagedFromGetAll(options, cancellationToken);
            return BuildSuccess(paged, EndpointNames.QueryPaged, StatusCodes.Status200OK, useProjection ? null : selectedFields);
        }

        var query = new GetPagedQuery<TResponse, TKey>(pageNumber, pageSize, request.Cacheable ?? false)
        {
            Filter = filterExpr,
            OrderBy = orderExpr,
            IncludeExpressions = includeExpressions,
            IncludeGraph = includeGraphValue,
            AsNoTracking = queryRequest.AsNoTracking,
            UseSplitQuery = queryRequest.UseSplitQuery
        };
        if (useProjection)
        {
            query.Selector = selector;
        }

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.QueryPaged, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleSeekAsync(KyrolusEfSeekQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var requestedIncludes = SplitCsv(parameters.Includes);
        var requestedFields = SplitCsv(parameters.Fields);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Seek,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(parameters.Filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.Seek, requestedIncludes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.Seek, SplitCsv(parameters.IncludeGraph), authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.Seek, requestedFields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;
        if (!TryResolveSeekProperties(out var seekProperties, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var (_, pageSize) = NormalizePaging(pageNumber: 1, parameters.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var includeDeleted = parameters.IncludeDeleted ?? false;
        var useProjection = TryBuildProjectionSelector(EndpointNames.Seek, selectedFields, out var selector);

        var query = new GetSeekQuery<TResponse, TKey>(pageSize, parameters.Cursor, parameters.Cacheable ?? false)
        {
            Filter = filterExpr,
            IncludeProperties = useStringIncludes ? includes : null,
            IncludeExpressions = includeExpressions,
            IncludeGraph = includeGraphValue,
            AsNoTracking = parameters.AsNoTracking,
            UseSplitQuery = parameters.UseSplitQuery,
            IncludeDeleted = includeDeleted,
            IncludeTotalCount = parameters.IncludeTotalCount ?? false,
            Descending = parameters.Descending ?? false,
            SeekPropertyNames = seekProperties
        };
        if (useProjection)
        {
            query.Selector = selector;
        }

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.Seek, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleQuerySeekAsync(KyrolusEfSeekQueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryRequest = request.Request ?? new QueryRequest();
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.QuerySeek,
            queryRequest.Fields,
            queryRequest.Includes,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.QuerySeek, queryRequest.Includes, authResult.AllowedIncludes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildIncludeGraph(EndpointNames.QuerySeek, queryRequest.IncludeGraph, authResult.AllowedIncludes, out var includeGraphValue, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.QuerySeek, queryRequest.Fields, authResult.AllowedFields, out var selectedFields, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;
        if (!TryResolveSeekProperties(out var seekProperties, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var (_, pageSize) = NormalizePaging(pageNumber: 1, request.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var includeDeleted = request.IncludeDeleted ?? false;
        var useProjection = TryBuildProjectionSelector(EndpointNames.QuerySeek, selectedFields, out var selector);

        var query = new GetSeekQuery<TResponse, TKey>(pageSize, request.Cursor, request.Cacheable ?? false)
        {
            Filter = filterExpr,
            IncludeProperties = useStringIncludes ? includes : null,
            IncludeExpressions = includeExpressions,
            IncludeGraph = includeGraphValue,
            AsNoTracking = queryRequest.AsNoTracking,
            UseSplitQuery = queryRequest.UseSplitQuery,
            IncludeDeleted = includeDeleted,
            IncludeTotalCount = request.IncludeTotalCount ?? false,
            Descending = request.Descending ?? false,
            SeekPropertyNames = seekProperties
        };
        if (useProjection)
        {
            query.Selector = selector;
        }

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.QuerySeek, StatusCodes.Status200OK, useProjection ? null : selectedFields);
    }

    public async Task<IResult> HandleCountAsync(string? filter = null, bool? includeDeleted = null, CancellationToken cancellationToken = default)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Count,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryBuildFilter(filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);

        var query = new CountQuery<TResponse>(cacheable: false)
        {
            Filter = filterExpr,
            IncludeDeleted = includeDeleted ?? false
        };

        var count = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(count, EndpointNames.Count, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleHeadByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Head,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: id,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out var errorResult)) return errorResult!;

        var query = config.QueryById;
        ApplyGetByIdQueryOptions(query, id, includeProperties: null, includeExpressions: null, includeGraph: null, asNoTracking: true, useSplitQuery: null);

        var result = await mediator.SendAsync(query, cancellationToken);
        if (result is null) return Results.NotFound();
        if (!TryEnsureTenantMatch(result, out errorResult)) return errorResult!;
        if (!TryEnsureRowAuthorization(result, authResult, out errorResult)) return errorResult!;

        TrySetEtagHeader(result);
        return Results.Ok();
    }

    public async Task<IResult> HandleBulkUpdateAsync(KyrolusEfBulkUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Updates is null || request.Updates.Count == 0)
        {
            return BuildBadRequest("Updates are required.");
        }

        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.BulkUpdate,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: request.Updates.Keys.ToArray(),
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryApplyPatchPermissions(request.Updates, authResult.AllowedPatchProperties, out var filteredUpdates, out var errorResult)) return errorResult!;
        if (filteredUpdates.Count == 0)
        {
            return BuildBadRequest("No update fields are allowed.");
        }

        var queryRequest = request.Request ?? new QueryRequest();
        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var command = efConfig?.ExecuteUpdateCommand
            ?? new ExecuteUpdateCommand<TResponse, TKey>(filterExpr, filteredUpdates, request.Cacheable ?? false, queryRequest.UseSplitQuery);

        ApplyCacheable(command, request.Cacheable);
        TrySetProperty(command, "Filter", filterExpr);
        TrySetProperty(command, "Updates", filteredUpdates);
        TrySetProperty(command, UseSplitQueryPropertyName, queryRequest.UseSplitQuery);

        var result = await mediator.SendAsync(command, cancellationToken);
        return BuildSuccess(result, EndpointNames.BulkUpdate, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleBulkDeleteAsync(KyrolusEfBulkDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.BulkDelete,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        var queryRequest = request.Request ?? new QueryRequest();
        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildContextFilter(out var contextFilter, out errorResult)) return errorResult!;

        filterExpr = CombineFilters(filterExpr, contextFilter);
        filterExpr = CombineFilters(filterExpr, authResult.RowFilter);
        var command = efConfig?.ExecuteDeleteCommand
            ?? new ExecuteDeleteCommand<TResponse, TKey>(filterExpr, request.Cacheable ?? false, queryRequest.UseSplitQuery);

        ApplyCacheable(command, request.Cacheable);
        TrySetProperty(command, "Filter", filterExpr);
        TrySetProperty(command, UseSplitQueryPropertyName, queryRequest.UseSplitQuery);

        var result = await mediator.SendAsync(command, cancellationToken);
        return BuildSuccess(result, EndpointNames.BulkDelete, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleBulkUpsertAsync(IAsyncEnumerable<TModel> models, bool? cacheable = null, CancellationToken cancellationToken = default)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.BulkUpsert,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryResolveSeekProperties(out var keyProperties, out var errorResult)) return errorResult!;
        var chunkSize = ResolveBulkChunkSize();
        var results = new List<TResponse>();

        await foreach (var chunk in ChunkAsync(models, chunkSize, cancellationToken))
        {
            if (chunk.Count == 0) continue;
            var entities = (IEnumerable<TResponse>)mapper.MapModelToEntity<TModel, TResponse>(chunk);
            if (!TryApplyContextValues(entities, out errorResult)) return errorResult!;

            var command = new BulkUpsertCommand<TResponse, TKey>(entities.ToList(), keyProperties, cacheable ?? false);
            ApplyCacheable(command, cacheable);

            var chunkResult = await mediator.SendAsync(command, cancellationToken);
            if (chunkResult is not null) results.AddRange(chunkResult);
        }

        return BuildSuccess(results, EndpointNames.BulkUpsert, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleBulkPatchAsync(IAsyncEnumerable<KyrolusEfBulkPatchItem> items, bool? cacheable = null, CancellationToken cancellationToken = default)
    {
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.BulkPatch,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        if (!TryRequireTenant(out var errorResult)) return errorResult!;

        var chunkSize = ResolveBulkChunkSize();
        var total = 0;
        await foreach (var chunk in ChunkAsync(items, chunkSize, cancellationToken))
        {
            if (chunk.Count == 0) continue;
            if (!TryParseBulkPatchChunk(chunk, authResult, out var parsed, out errorResult)) return errorResult!;
            if (parsed.Count == 0) continue;

            var command = new BulkPatchCommand<TResponse, TKey>(parsed, cacheable ?? false);
            ApplyCacheable(command, cacheable);

            total += await mediator.SendAsync(command, cancellationToken);
        }

        return BuildSuccess(total, EndpointNames.BulkPatch, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleBatchAsync(KyrolusBatchRequest<TModel, TKey> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate batch options
        if (efConfig?.BatchOptions is null || !efConfig.BatchOptions.Enabled)
        {
            return BuildBadRequest("Batch operations are not enabled for this endpoint.");
        }

        var batchOptions = efConfig.BatchOptions;
        if (request.Operations.Count == 0)
        {
            return BuildBadRequest("No operations provided.");
        }

        if (request.Operations.Count > batchOptions.MaxOperationsPerBatch)
        {
            return BuildBadRequest($"Too many operations. Maximum allowed is {batchOptions.MaxOperationsPerBatch}.");
        }

        // Authorize batch endpoint
        var authResult = await ResolveAuthorizationAsync(
            EndpointNames.Batch,
            requestedFields: null,
            requestedIncludes: null,
            requestedPatchProperties: null,
            resourceId: null,
            keyValues: null,
            cancellationToken).ConfigureAwait(false);
        if (!authResult.IsAuthorized) return BuildAuthorizationError(authResult);

        // Check atomic mode
        var useAtomic = request.Atomic;
        if (useAtomic && !batchOptions.AllowNonAtomic && !request.Atomic)
        {
            useAtomic = true; // Force atomic if non-atomic not allowed
        }

        var results = new List<KyrolusBatchOperationResult<TResponse, TKey>>();
        var shouldContinue = true;

        foreach (var operation in request.Operations)
        {
            if (!shouldContinue)
            {
                results.Add(KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                    operation.OperationId,
                    operation.Operation,
                    operation.Id,
                    StatusCodes.Status400BadRequest,
                    "SKIPPED",
                    "Operation skipped due to previous failure."));
                continue;
            }

            // Validate operation type is allowed
            if (!batchOptions.AllowedOperations.Contains(operation.Operation))
            {
                var opResult = KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                    operation.OperationId,
                    operation.Operation,
                    operation.Id,
                    StatusCodes.Status400BadRequest,
                    "OPERATION_NOT_ALLOWED",
                    $"Operation type '{operation.Operation}' is not allowed.");
                results.Add(opResult);

                if (!request.ContinueOnError && !operation.ContinueOnError)
                {
                    shouldContinue = false;
                }
                continue;
            }

            var result = await ExecuteBatchOperationAsync(operation, request.ReturnData, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            if (!result.Success && !request.ContinueOnError && !operation.ContinueOnError)
            {
                shouldContinue = false;
            }
        }

        var response = KyrolusBatchResponse<TResponse, TKey>.FromResults(results);
        var statusCode = response.Success ? StatusCodes.Status200OK : StatusCodes.Status207MultiStatus;
        return Results.Json(response, statusCode: statusCode);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchOperationAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
    {
        try
        {
            return operation.Operation switch
            {
                KyrolusBatchOperationType.Create => await ExecuteBatchCreateAsync(operation, returnData, cancellationToken),
                KyrolusBatchOperationType.Update => await ExecuteBatchUpdateAsync(operation, returnData, cancellationToken),
                KyrolusBatchOperationType.Patch => await ExecuteBatchPatchAsync(operation, returnData, cancellationToken),
                KyrolusBatchOperationType.Delete => await ExecuteBatchDeleteAsync(operation, cancellationToken),
                KyrolusBatchOperationType.Upsert => await ExecuteBatchUpsertAsync(operation, returnData, cancellationToken),
                _ => KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                    operation.OperationId,
                    operation.Operation,
                    operation.Id,
                    StatusCodes.Status400BadRequest,
                    "UNKNOWN_OPERATION",
                    $"Unknown operation type: {operation.Operation}")
            };
        }
        catch (DbUpdateConcurrencyException)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status409Conflict,
                "CONCURRENCY_CONFLICT",
                "Concurrency conflict.");
        }
        catch (Exception ex)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR",
                ex.Message);
        }
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchCreateAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
    {
        if (operation.Data is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_DATA",
                "Data is required for create operation.");
        }

        var validationResult = await ValidateBatchModelAsync(operation.Data, cancellationToken).ConfigureAwait(false);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(operation.Data);
        if (!TryApplyContextValues(entity, out _))
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "CONTEXT_ERROR",
                "Failed to apply context values.");
        }

        var command = config.AddCommand;
        TrySetProperty(command, "Entity", entity);
        var result = await mediator.SendAsync(command, cancellationToken);
        var resultId = TryGetEntityId(result);

        return KyrolusBatchOperationResult<TResponse, TKey>.Succeeded(
            operation.OperationId,
            operation.Operation,
            resultId,
            StatusCodes.Status201Created,
            returnData ? result : default);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchUpdateAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
    {
        if (operation.Id is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_ID",
                "ID is required for update operation.");
        }

        if (operation.Data is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_DATA",
                "Data is required for update operation.");
        }

        var validationResult = await ValidateBatchModelAsync(operation.Data, cancellationToken).ConfigureAwait(false);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(operation.Data);
        if (!TrySetEntityId(entity, operation.Id, out _))
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "ID_ERROR",
                "Failed to set entity ID.");
        }

        if (!TryApplyContextValues(entity, out _))
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "CONTEXT_ERROR",
                "Failed to apply context values.");
        }

        var command = config.UpdateCommand;
        TrySetProperty(command, "Entity", entity);
        var result = await mediator.SendAsync(command, cancellationToken);

        return KyrolusBatchOperationResult<TResponse, TKey>.Succeeded(
            operation.OperationId,
            operation.Operation,
            operation.Id,
            StatusCodes.Status200OK,
            returnData ? result : default);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchPatchAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
    {
        if (operation.Id is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_ID",
                "ID is required for patch operation.");
        }

        if (operation.Data is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_DATA",
                "Data is required for patch operation.");
        }

        // Convert model to dictionary of updates
        var updates = ConvertModelToUpdates(operation.Data);
        if (updates.Count == 0)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "NO_UPDATES",
                "No update fields provided.");
        }

        var keyValues = BuildKeyValues(operation.Id);
        var command = config.PatchCommand;
        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        TrySetProperty(command, "Updates", updates);

        var result = await mediator.SendAsync(command, cancellationToken);

        return KyrolusBatchOperationResult<TResponse, TKey>.Succeeded(
            operation.OperationId,
            operation.Operation,
            operation.Id,
            StatusCodes.Status200OK,
            returnData ? result : default);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchDeleteAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        CancellationToken cancellationToken)
    {
        if (operation.Id is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_ID",
                "ID is required for delete operation.");
        }

        var keyValues = BuildKeyValues(operation.Id);
        IKyrolusCommandBase command = config.RemoveCommand;
        if (efConfig?.UseSoftDeleteForDelete == true)
        {
            command = new SoftDeleteByIdCommand<TResponse, TKey>(keyValues, false);
        }

        TrySetProperty(command, KeyValuesPropertyName, keyValues);
        await SendCommandAsync(command, cancellationToken);

        return KyrolusBatchOperationResult<TResponse, TKey>.Succeeded(
            operation.OperationId,
            operation.Operation,
            operation.Id,
            StatusCodes.Status200OK);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>> ExecuteBatchUpsertAsync(
        KyrolusBatchOperation<TModel, TKey> operation,
        bool returnData,
        CancellationToken cancellationToken)
    {
        if (operation.Data is null)
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "MISSING_DATA",
                "Data is required for upsert operation.");
        }

        var validationResult = await ValidateBatchModelAsync(operation.Data, cancellationToken).ConfigureAwait(false);
        if (validationResult is not null)
        {
            return validationResult;
        }

        var entity = (TResponse)mapper.MapModelToEntity<TModel, TResponse>(operation.Data);
        if (operation.Id is not null)
        {
            TrySetEntityId(entity, operation.Id, out _);
        }

        if (!TryApplyContextValues(entity, out _))
        {
            return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
                operation.OperationId,
                operation.Operation,
                operation.Id,
                StatusCodes.Status400BadRequest,
                "CONTEXT_ERROR",
                "Failed to apply context values.");
        }

        // Check if entity exists
        var existingId = operation.Id ?? TryGetEntityId(entity);
        TResponse result;
        int statusCode;

        if (existingId is not null && !existingId.Equals(default(TKey)))
        {
            // Try to load existing
            var query = config.QueryById;
            ApplyGetByIdQueryOptions(query, existingId, includeProperties: null, includeExpressions: null, includeGraph: null, asNoTracking: true, useSplitQuery: null);
            var existing = await mediator.SendAsync(query, cancellationToken);

            if (existing is not null)
            {
                // Update
                var updateCommand = config.UpdateCommand;
                TrySetProperty(updateCommand, "Entity", entity);
                result = await mediator.SendAsync(updateCommand, cancellationToken);
                statusCode = StatusCodes.Status200OK;
            }
            else
            {
                // Create
                var addCommand = config.AddCommand;
                TrySetProperty(addCommand, "Entity", entity);
                result = await mediator.SendAsync(addCommand, cancellationToken);
                statusCode = StatusCodes.Status201Created;
            }
        }
        else
        {
            // Create new
            var addCommand = config.AddCommand;
            TrySetProperty(addCommand, "Entity", entity);
            result = await mediator.SendAsync(addCommand, cancellationToken);
            statusCode = StatusCodes.Status201Created;
        }

        var resultId = TryGetEntityId(result) ?? operation.Id;
        return KyrolusBatchOperationResult<TResponse, TKey>.Succeeded(
            operation.OperationId,
            operation.Operation,
            resultId,
            statusCode,
            returnData ? result : default);
    }

    private async Task<KyrolusBatchOperationResult<TResponse, TKey>?> ValidateBatchModelAsync(
        TModel model,
        CancellationToken cancellationToken)
    {
        if (validationEngine is null) return null;
        var failures = await validationEngine.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
        if (failures.Count == 0) return null;

        var details = failures
            .Select(f => new KyrolusBatchErrorDetail(f.FieldPath ?? f.PropertyName, f.ErrorCode ?? "VALIDATION_ERROR", f.ErrorMessage))
            .ToList();

        return KyrolusBatchOperationResult<TResponse, TKey>.Failed(
            null,
            KyrolusBatchOperationType.Create,
            default,
            StatusCodes.Status400BadRequest,
            "VALIDATION_ERROR",
            "Validation failed.",
            details);
    }

    private TKey? TryGetEntityId(TResponse? entity)
    {
        if (entity is null) return default;
        var idProperty = typeof(TResponse).GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
        if (idProperty is null) return default;
        var value = idProperty.GetValue(entity);
        return value is TKey key ? key : default;
    }

    private static Dictionary<string, object> ConvertModelToUpdates(TModel model)
    {
        var updates = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var prop in properties)
        {
            if (!prop.CanRead) continue;
            var value = prop.GetValue(model);
            if (value is not null)
            {
                updates[prop.Name] = value;
            }
        }
        return updates;
    }

    private sealed record GetAllQueryOptions(
        Expression<Func<TResponse, bool>>? Filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy,
        List<string>? IncludeProperties,
        Expression<Func<TResponse, object?>>[]? IncludeExpressions,
        IncludeGraph<TResponse>? IncludeGraph,
        bool? AsNoTracking,
        bool? UseSplitQuery);

    private bool TryParseBulkPatchChunk(
        IReadOnlyList<KyrolusEfBulkPatchItem> chunk,
        KyrolusEfAuthorizationResult<TResponse> authResult,
        out List<KyrolusBulkPatchItem> parsed,
        out IResult? errorResult)
    {
        parsed = new List<KyrolusBulkPatchItem>(chunk.Count);
        errorResult = null;
        foreach (var item in chunk)
        {
            if (!TryParseBulkPatchItem(item, authResult, out var parsedItem, out var skip, out errorResult))
            {
                return false;
            }

            if (skip || parsedItem is null) continue;
            parsed.Add(parsedItem);
        }

        return true;
    }

    private bool TryParseBulkPatchItem(
        KyrolusEfBulkPatchItem item,
        KyrolusEfAuthorizationResult<TResponse> authResult,
        out KyrolusBulkPatchItem? parsedItem,
        out bool skip,
        out IResult? errorResult)
    {
        parsedItem = null;
        skip = false;
        errorResult = null;

        if (item.Updates is null || item.Updates.Count == 0)
        {
            errorResult = BuildBadRequest("Updates are required.");
            return false;
        }

        if (!TryRejectContextUpdates(item.Updates, out errorResult)) return false;
        if (!TryApplyPatchPermissions(item.Updates, authResult.AllowedPatchProperties, out var filteredUpdates, out errorResult)) return false;

        var keys = ResolveBulkPatchKeys(item);
        if (!TryBuildKeyValues(keys, out var keyValues, out errorResult)) return false;

        if (filteredUpdates.Count == 0)
        {
            if (efConfig?.StrictPatchValidation == true)
            {
                errorResult = BuildBadRequest("No patch fields are allowed.");
                return false;
            }
            skip = true;
            return true;
        }

        parsedItem = new KyrolusBulkPatchItem(keyValues, filteredUpdates);
        return true;
    }

    private static string[]? ResolveBulkPatchKeys(KyrolusEfBulkPatchItem item)
    {
        if (item.Keys is { Length: > 0 })
        {
            return item.Keys;
        }

        if (!string.IsNullOrWhiteSpace(item.Id))
        {
            return [item.Id];
        }

        return item.Keys;
    }

    private sealed record GetAllPagedOptions(
        Expression<Func<TResponse, bool>>? Filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy,
        List<string>? IncludeProperties,
        IncludeGraph<TResponse>? IncludeGraph,
        bool? AsNoTracking,
        bool? UseSplitQuery,
        int PageNumber,
        int PageSize,
        bool? Cacheable,
        bool IncludeDeleted,
        Expression<Func<TResponse, TResponse>>? Selector);

    private async Task<KyrolusPagedResult<TResponse>> BuildPagedFromGetAll(
        GetAllPagedOptions options,
        CancellationToken cancellationToken)
    {
        var getAllQuery = new GetAllQuery<TResponse>(options.Cacheable ?? false)
        {
            Filter = options.Filter,
            OrderBy = options.OrderBy,
            IncludeProperties = options.IncludeProperties,
            IncludeGraph = options.IncludeGraph,
            AsNoTracking = options.AsNoTracking,
            UseSplitQuery = options.UseSplitQuery
        };
        getAllQuery.IncludeDeleted = options.IncludeDeleted;
        if (options.Selector is not null)
        {
            getAllQuery.Selector = options.Selector;
        }

        var items = await mediator.SendAsync(getAllQuery, cancellationToken);
        var list = items?.ToList() ?? [];
        var total = list.Count;
        var pageItems = list.Skip((options.PageNumber - 1) * options.PageSize).Take(options.PageSize).ToList();
        return new KyrolusPagedResult<TResponse>(pageItems, total, options.PageNumber, options.PageSize);
    }

    private static List<string>? SplitCsv(string? csv)
        => string.IsNullOrWhiteSpace(csv)
            ? null
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private async Task<IResult?> ValidateModelAsync(TModel model, CancellationToken cancellationToken)
    {
        if (validationEngine is null) return null;
        var failures = await validationEngine.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
        return failures.Count == 0 ? null : BuildValidationError(failures);
    }

    private async Task<IResult?> ValidateModelRangeAsync(IEnumerable<TModel> models, CancellationToken cancellationToken)
    {
        if (validationEngine is null) return null;
        var allFailures = new List<KyrolusValidationFailure>();
        foreach (var model in models)
        {
            var failures = await validationEngine.ValidateAsync(model, cancellationToken).ConfigureAwait(false);
            if (failures.Count > 0) allFailures.AddRange(failures);
        }
        return allFailures.Count == 0 ? null : BuildValidationError(allFailures);
    }

    private async ValueTask<KyrolusEfAuthorizationResult<TResponse>> ResolveAuthorizationAsync(
        EndpointNames endpoint,
        IReadOnlyCollection<string>? requestedFields,
        IReadOnlyCollection<string>? requestedIncludes,
        IReadOnlyCollection<string>? requestedPatchProperties,
        object? resourceId,
        object?[]? keyValues,
        CancellationToken cancellationToken)
    {
        var httpContext = HttpContext;
        var context = new KyrolusEfAuthorizationContext<TResponse>(
            endpoint,
            httpContext?.Request?.Method,
            httpContext?.Request?.Path.Value,
            httpContext?.User,
            httpContext,
            endpointContext?.TenantId,
            endpointContext?.ScopeKey,
            requestedFields,
            requestedIncludes,
            requestedPatchProperties,
            resourceId,
            keyValues);
        return await authorizationProvider.AuthorizeAsync(context, cancellationToken).ConfigureAwait(false);
    }

    private IResult BuildAuthorizationError(KyrolusEfAuthorizationResult<TResponse> authResult)
        => authResult.ReturnNotFound
            ? BuildNotFound()
            : BuildForbidden(authResult.ErrorMessage);

    private bool TryRequireTenant(out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig?.RequireTenant != true) return true;
        if (!string.IsNullOrWhiteSpace(endpointContext?.TenantId)) return true;
        errorResult = BuildBadRequest("Tenant id is required.");
        return false;
    }

    private bool TryBuildContextFilter(out Expression<Func<TResponse, bool>>? expression, out IResult? errorResult)
    {
        errorResult = null;
        expression = null;
        if (efConfig is null) return true;

        if (!TryBuildPropertyFilter(efConfig.TenantPropertyName, endpointContext?.TenantId, efConfig.RequireTenant, "tenant", out var tenantFilter, out errorResult))
        {
            return false;
        }

        if (!TryBuildPropertyFilter(efConfig.ScopePropertyName, endpointContext?.ScopeKey, required: false, "scope", out var scopeFilter, out errorResult))
        {
            return false;
        }

        expression = CombineFilters(tenantFilter, scopeFilter);
        return true;
    }

    private bool TryBuildPropertyFilter(
        string? propertyName,
        string? rawValue,
        bool required,
        string label,
        out Expression<Func<TResponse, bool>>? expression,
        out IResult? errorResult)
    {
        errorResult = null;
        expression = null;
        if (string.IsNullOrWhiteSpace(propertyName)) return true;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (required)
            {
                errorResult = BuildBadRequest($"{label} is required.");
                return false;
            }
            return true;
        }

        var prop = typeof(TResponse).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null)
        {
            errorResult = BuildBadRequest($"Property '{propertyName}' was not found on {typeof(TResponse).Name}.");
            return false;
        }

        object? converted;
        try
        {
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            converted = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            errorResult = BuildBadRequest($"Invalid {label} value.");
            return false;
        }

        var parameter = Expression.Parameter(typeof(TResponse), "e");
        var member = Expression.Property(parameter, prop);
        var constant = Expression.Constant(converted, prop.PropertyType);
        expression = Expression.Lambda<Func<TResponse, bool>>(Expression.Equal(member, constant), parameter);
        return true;
    }

    private static Expression<Func<TResponse, bool>>? CombineFilters(Expression<Func<TResponse, bool>>? first, Expression<Func<TResponse, bool>>? second)
    {
        if (first is null) return second;
        if (second is null) return first;
        var parameter = Expression.Parameter(typeof(TResponse), "e");
        var left = new ReplaceParameterVisitor(first.Parameters[0], parameter).Visit(first.Body)!;
        var right = new ReplaceParameterVisitor(second.Parameters[0], parameter).Visit(second.Body)!;
        return Expression.Lambda<Func<TResponse, bool>>(Expression.AndAlso(left, right), parameter);
    }

    private sealed class ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target) : ExpressionVisitor
    {
        private readonly ParameterExpression source = source;
        private readonly ParameterExpression target = target;

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }

    private bool HasContextFilters()
        => !string.IsNullOrWhiteSpace(efConfig?.TenantPropertyName)
           || !string.IsNullOrWhiteSpace(efConfig?.ScopePropertyName);

    private bool TryApplyContextValues(TResponse entity, out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig is null) return true;
        if (!TryApplyContextValue(entity, efConfig.TenantPropertyName, endpointContext?.TenantId, efConfig.RequireTenant, "tenant", out errorResult))
        {
            return false;
        }

        if (!TryApplyContextValue(entity, efConfig.ScopePropertyName, endpointContext?.ScopeKey, required: false, "scope", out errorResult))
        {
            return false;
        }

        return true;
    }

    private bool TryApplyContextValues(IEnumerable<TResponse> entities, out IResult? errorResult)
    {
        foreach (var entity in entities)
        {
            if (!TryApplyContextValues(entity, out errorResult)) return false;
        }

        errorResult = null;
        return true;
    }

    private bool TryApplyContextValue(object target, string? propertyName, string? rawValue, bool required, string label, out IResult? errorResult)
    {
        errorResult = null;
        if (string.IsNullOrWhiteSpace(propertyName)) return true;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (required)
            {
                errorResult = BuildBadRequest($"{label} is required.");
                return false;
            }
            return true;
        }

        if (!TrySetPropertyValue(target, propertyName, rawValue))
        {
            errorResult = BuildBadRequest($"Cannot set {label}.");
            return false;
        }

        return true;
    }

    private bool TryRejectContextUpdates(IReadOnlyDictionary<string, object> updates, out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig is null) return true;
        if (!string.IsNullOrWhiteSpace(efConfig.TenantPropertyName)
            && updates.Keys.Any(k => string.Equals(k, efConfig.TenantPropertyName, StringComparison.OrdinalIgnoreCase)))
        {
            errorResult = BuildBadRequest("Tenant cannot be updated.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(efConfig.ScopePropertyName)
            && updates.Keys.Any(k => string.Equals(k, efConfig.ScopePropertyName, StringComparison.OrdinalIgnoreCase)))
        {
            errorResult = BuildBadRequest("Scope cannot be updated.");
            return false;
        }

        return true;
    }

    private bool TryApplyPatchPermissions(
        IReadOnlyDictionary<string, object> updates,
        IReadOnlyCollection<string>? allowedPatchProperties,
        out Dictionary<string, object> filtered,
        out IResult? errorResult)
    {
        errorResult = null;
        filtered = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        if (updates.Count == 0)
        {
            return true;
        }

        var allowlist = BuildAllowlist(MergeAllowlist(efConfig?.AllowedPatchProperties, allowedPatchProperties));
        var strict = efConfig?.StrictPatchValidation ?? false;
        foreach (var (key, value) in updates)
        {
            if (allowlist is not null && !allowlist.Contains(key))
            {
                if (strict)
                {
                    errorResult = BuildBadRequest($"Patch field '{key}' is not allowed.");
                    return false;
                }
                continue;
            }

            filtered[key] = value;
        }

        return true;
    }

    private bool TryEnsureTenantMatch(TResponse entity, out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig is null) return true;
        if (!TryRequireTenant(out errorResult)) return false;

        if (!TryEnsureContextMatch(entity, efConfig.TenantPropertyName, endpointContext?.TenantId, "tenant", out errorResult))
        {
            return false;
        }

        if (!TryEnsureContextMatch(entity, efConfig.ScopePropertyName, endpointContext?.ScopeKey, "scope", out errorResult))
        {
            return false;
        }

        return true;
    }

    private bool TryEnsureRowAuthorization(TResponse entity, KyrolusEfAuthorizationResult<TResponse> authResult, out IResult? errorResult)
    {
        errorResult = null;
        if (authResult.RowFilter is null) return true;
        try
        {
            var predicate = authResult.RowFilter.Compile();
            if (predicate(entity)) return true;
        }
        catch
        {
            // If the filter can't be evaluated on the materialized entity, treat as forbidden.
        }

        errorResult = BuildAuthorizationError(authResult);
        return false;
    }

    private bool TryEnsureContextMatch(TResponse entity, string? propertyName, string? rawValue, string label, out IResult? errorResult)
    {
        errorResult = null;
        if (string.IsNullOrWhiteSpace(propertyName) || string.IsNullOrWhiteSpace(rawValue)) return true;

        var prop = typeof(TResponse).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null)
        {
            errorResult = BuildBadRequest($"Property '{propertyName}' was not found on {typeof(TResponse).Name}.");
            return false;
        }

        object? converted;
        try
        {
            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
            converted = Convert.ChangeType(rawValue, targetType, CultureInfo.InvariantCulture);
        }
        catch
        {
            errorResult = BuildBadRequest($"Invalid {label} value.");
            return false;
        }

        var current = prop.GetValue(entity);
        if (!Equals(current, converted))
        {
            errorResult = BuildNotFound();
            return false;
        }

        return true;
    }

    private async Task<(bool Success, IResult? Error)> TryEnsureAccessAsync(
        TKey id,
        bool includeDeleted,
        bool? cacheable,
        KyrolusEfAuthorizationResult<TResponse> authResult)
    {
        if (!HasContextFilters() && authResult.RowFilter is null) return (true, null);
        var entity = await LoadByIdAsync(id, includeDeleted, cacheable).ConfigureAwait(false);
        if (entity is null)
        {
            return (false, BuildNotFound());
        }

        if (!TryEnsureTenantMatch(entity, out var errorResult)) return (false, errorResult);
        if (!TryEnsureRowAuthorization(entity, authResult, out errorResult)) return (false, errorResult);
        return (true, null);
    }

    private async Task<(bool Success, IResult? Error)> TryEnsureAccessAsync(
        object?[] keyValues,
        bool includeDeleted,
        bool? cacheable,
        KyrolusEfAuthorizationResult<TResponse> authResult)
    {
        if (!HasContextFilters() && authResult.RowFilter is null) return (true, null);
        var entity = await LoadByKeysAsync(keyValues, includeDeleted, cacheable).ConfigureAwait(false);
        if (entity is null)
        {
            return (false, BuildNotFound());
        }

        if (!TryEnsureTenantMatch(entity, out var errorResult)) return (false, errorResult);
        if (!TryEnsureRowAuthorization(entity, authResult, out errorResult)) return (false, errorResult);
        return (true, null);
    }

    private async Task<TResponse?> LoadByIdAsync(TKey id, bool includeDeleted, bool? cacheable)
    {
        var query = config.QueryById;
        ApplyCacheable(query, cacheable);
        ApplyGetByIdQueryOptions(query, id, includeProperties: null, includeExpressions: null, includeGraph: null, asNoTracking: true, useSplitQuery: null);
        if (includeDeleted)
        {
            TrySetProperty(query, IncludeDeletedPropertyName, true);
        }
        return await mediator.SendAsync(query, CancellationToken.None).ConfigureAwait(false);
    }

    private async Task<TResponse?> LoadByKeysAsync(object?[] keyValues, bool includeDeleted, bool? cacheable)
    {
        var query = efConfig?.QueryByKeyValues ?? new GetByKeyValuesQuery<TResponse, TKey>(keyValues, cacheable ?? false);
        ApplyCacheable(query, cacheable);
        ApplyGetByKeyValuesQueryOptions(query, keyValues, includeProperties: null, includeExpressions: null, includeGraph: null, asNoTracking: true, useSplitQuery: null);
        if (includeDeleted)
        {
            TrySetProperty(query, IncludeDeletedPropertyName, true);
        }
        return await mediator.SendAsync(query, CancellationToken.None).ConfigureAwait(false);
    }

    private bool TryBuildProjectionSelector(EndpointNames endpoint, IReadOnlyList<string>? fields, out Expression<Func<TResponse, TResponse>>? selector)
    {
        selector = null;
        if (fields is null || fields.Count == 0) return false;
        if (fields.Any(f => f.Contains('.', StringComparison.Ordinal))) return false;
        var viewModelType = ResolveViewModelType(endpoint);
        if (viewModelType != typeof(TResponse)) return false;

        var parameter = Expression.Parameter(typeof(TResponse), "e");
        var bindings = new List<MemberBinding>();
        foreach (var field in fields)
        {
            var prop = typeof(TResponse).GetProperty(field, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null || !prop.CanWrite) continue;
            bindings.Add(Expression.Bind(prop, Expression.Property(parameter, prop)));
        }

        if (bindings.Count == 0) return false;
        var body = Expression.MemberInit(Expression.New(typeof(TResponse)), bindings);
        selector = Expression.Lambda<Func<TResponse, TResponse>>(body, parameter);
        return true;
    }

    private bool TryApplyIfMatch(TResponse entity, out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig?.EnableEtags != true) return true;
        if (!TryGetRowVersionProperty(out var rowVersionProperty)) return true;
        if (!TryGetIfMatchValues(out var ifMatchValues)) return true;
        var matchValue = ifMatchValues.FirstOrDefault(v => v != "*");
        if (string.IsNullOrWhiteSpace(matchValue)) return true;

        if (!TryParseEtagValue(matchValue, rowVersionProperty.PropertyType, out var parsed))
        {
            errorResult = BuildBadRequest("Invalid If-Match header.");
            return false;
        }

        if (!TrySetPropertyValue(entity, rowVersionProperty.Name, parsed))
        {
            errorResult = BuildBadRequest("Invalid If-Match value.");
            return false;
        }

        return true;
    }

    private async Task<(bool Success, IResult? Error)> TryEnsureIfMatchAsync(TKey id, bool? cacheable)
    {
        if (efConfig?.EnableEtags != true) return (true, null);
        if (!TryGetIfMatchValues(out var ifMatchValues)) return (true, null);
        var entity = await LoadByIdAsync(id, includeDeleted: false, cacheable).ConfigureAwait(false);
        if (entity is null)
        {
            return (false, BuildNotFound());
        }

        if (!TryGetEtagValue(entity, out var etag)) return (true, null);
        if (ifMatchValues.Any(v => v == "*" || string.Equals(v, etag, StringComparison.Ordinal)))
        {
            return (true, null);
        }

        return (false, BuildConflict(ConcurrencyConflictMessage));
    }

    private async Task<(bool Success, IResult? Error)> TryEnsureIfMatchAsync(object?[] keyValues, bool? cacheable)
    {
        if (efConfig?.EnableEtags != true) return (true, null);
        if (!TryGetIfMatchValues(out var ifMatchValues)) return (true, null);
        var entity = await LoadByKeysAsync(keyValues, includeDeleted: false, cacheable).ConfigureAwait(false);
        if (entity is null)
        {
            return (false, BuildNotFound());
        }

        if (!TryGetEtagValue(entity, out var etag)) return (true, null);
        if (ifMatchValues.Any(v => v == "*" || string.Equals(v, etag, StringComparison.Ordinal)))
        {
            return (true, null);
        }

        return (false, BuildConflict(ConcurrencyConflictMessage));
    }

    private bool TryBuildNotModifiedResult(TResponse entity, out IResult result)
    {
        result = null!;
        if (efConfig?.EnableEtags != true) return false;
        if (!TryGetIfNoneMatchValues(out var ifNoneMatchValues)) return false;
        if (!TryGetEtagValue(entity, out var etag)) return false;
        if (!ifNoneMatchValues.Any(v => v == "*" || string.Equals(v, etag, StringComparison.Ordinal))) return false;

        TrySetEtagHeader(etag);
        result = Results.StatusCode(StatusCodes.Status304NotModified);
        return true;
    }

    private void TrySetEtagHeader(TResponse entity)
    {
        if (TryGetEtagValue(entity, out var etag))
        {
            TrySetEtagHeader(etag);
        }
    }

    private void TrySetEtagHeader(string etag)
    {
        if (HttpContext is null) return;
        if (string.IsNullOrWhiteSpace(etag)) return;
        HttpContext.Response.Headers.ETag = $"\"{etag}\"";
    }

    private bool TryGetEtagValue(TResponse entity, out string etag)
    {
        etag = string.Empty;
        if (efConfig?.EnableEtags != true) return false;
        if (!TryGetRowVersionProperty(out var rowVersionProperty)) return false;
        var value = rowVersionProperty.GetValue(entity);
        if (value is null) return false;
        etag = NormalizeEtagValue(value);
        return !string.IsNullOrWhiteSpace(etag);
    }

    private bool TryGetRowVersionProperty(out PropertyInfo rowVersionProperty)
    {
        rowVersionProperty = null!;
        if (string.IsNullOrWhiteSpace(efConfig?.RowVersionPropertyName)) return false;
        var prop = typeof(TResponse).GetProperty(efConfig.RowVersionPropertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (prop is null) return false;
        rowVersionProperty = prop;
        return true;
    }

    private static string NormalizeEtagValue(object value)
    {
        return value switch
        {
            byte[] bytes => Convert.ToBase64String(bytes),
            Guid guid => guid.ToString("N"),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private bool TryParseEtagValue(string raw, Type targetType, out object? value)
    {
        value = null;
        var normalized = NormalizeEtagHeader(raw);
        if (string.IsNullOrWhiteSpace(normalized)) return false;

        var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try
        {
            if (nonNullable == typeof(byte[]))
            {
                value = Convert.FromBase64String(normalized);
                return true;
            }
            if (nonNullable == typeof(Guid))
            {
                value = Guid.Parse(normalized);
                return true;
            }
            if (nonNullable == typeof(string))
            {
                value = normalized;
                return true;
            }

            value = Convert.ChangeType(normalized, nonNullable, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool TryGetIfMatchValues(out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        var context = HttpContext;
        if (context is null) return false;
        if (!context.Request.Headers.TryGetValue("If-Match", out var header)) return false;
        values = SplitEtags(header.ToString());
        return values.Count > 0;
    }

    private bool TryGetIfNoneMatchValues(out IReadOnlyList<string> values)
    {
        values = Array.Empty<string>();
        var context = HttpContext;
        if (context is null) return false;
        if (!context.Request.Headers.TryGetValue("If-None-Match", out var header)) return false;
        values = SplitEtags(header.ToString());
        return values.Count > 0;
    }

    private static IReadOnlyList<string> SplitEtags(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        var list = new List<string>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var normalized = NormalizeEtagHeader(part);
            if (!string.IsNullOrWhiteSpace(normalized)) list.Add(normalized);
        }
        return list;
    }

    private static string NormalizeEtagHeader(string raw)
    {
        var value = raw.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value[2..];
        }

        value = value.Trim();
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            value = value[1..^1];
        }

        return value.Trim();
    }

    private bool TryBuildFilter(string? filter, out Expression<Func<TResponse, bool>>? expression, out IResult? errorResult)
    {
        errorResult = null;
        var strict = efConfig?.StrictFilterValidation ?? false;
        var caseInsensitive = efConfig?.FilterCaseInsensitive ?? false;
        if (!FilterBuilder.TryBuildFilterExpression<TResponse>(filter, BuildAllowlist(efConfig?.AllowedFilterProperties), strict, caseInsensitive, out expression, out var error))
        {
            errorResult = BuildBadRequest(error ?? "Invalid filter.");
            return false;
        }

        if (error is not null && strict)
        {
            errorResult = BuildBadRequest(error);
            return false;
        }

        return true;
    }

    private bool TryBuildFilter(IReadOnlyList<FilterClause>? clauses, out Expression<Func<TResponse, bool>>? expression, out IResult? errorResult)
    {
        errorResult = null;
        var strict = efConfig?.StrictFilterValidation ?? false;
        var caseInsensitive = efConfig?.FilterCaseInsensitive ?? false;
        if (!FilterBuilder.TryBuildFilterExpression<TResponse>(clauses, BuildAllowlist(efConfig?.AllowedFilterProperties), strict, caseInsensitive, out expression, out var error))
        {
            errorResult = BuildBadRequest(error ?? "Invalid filter.");
            return false;
        }

        if (error is not null && strict)
        {
            errorResult = BuildBadRequest(error);
            return false;
        }

        return true;
    }

    private bool TryBuildOrder(string? orderBy, out Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? orderExpr, out IResult? errorResult)
    {
        var strict = efConfig?.StrictFilterValidation ?? false;
        orderExpr = OrderBuilder.BuildOrderBy<TResponse>(orderBy, BuildAllowlist(efConfig?.AllowedOrderProperties), strict, out var error);
        if (error is null) { errorResult = null; return true; }

        errorResult = BuildBadRequest(error);
        return false;
    }

    private bool TryBuildOrder(IReadOnlyList<OrderClause>? clauses, out Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? orderExpr, out IResult? errorResult)
    {
        var strict = efConfig?.StrictFilterValidation ?? false;
        orderExpr = OrderBuilder.BuildOrderBy<TResponse>(clauses, BuildAllowlist(efConfig?.AllowedOrderProperties), strict, out var error);
        if (error is null) { errorResult = null; return true; }

        errorResult = BuildBadRequest(error);
        return false;
    }

    private bool TryBuildIncludes(
        EndpointNames endpoint,
        IEnumerable<string>? requested,
        IReadOnlyCollection<string>? allowedIncludes,
        out List<string>? includes,
        out IResult? errorResult)
    {
        errorResult = null;
        var endpointIncludes = GetEndpointConfig(endpoint)?.IncludeProps;
        var merged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (endpointIncludes is not null)
        {
            foreach (var inc in endpointIncludes.Where(static inc => !string.IsNullOrWhiteSpace(inc)))
            {
                merged.Add(inc);
            }
        }

        if (requested is not null)
        {
            foreach (var inc in requested.Where(static inc => !string.IsNullOrWhiteSpace(inc)))
            {
                merged.Add(inc);
            }
        }

        if (merged.Count == 0)
        {
            includes = null;
            return true;
        }

        var strict = efConfig?.StrictIncludeValidation ?? false;
        var allowlist = BuildAllowlist(MergeAllowlist(efConfig?.AllowedIncludeProperties, allowedIncludes));
        includes = KyrolusSousRoutingHelpers.GetIncludedProperties(
            merged,
            allowlist,
            strict,
            out var error);

        if (error is null) return true;

        errorResult = BuildBadRequest(error);
        return false;
    }

    private bool TryBuildFields(
        EndpointNames endpoint,
        IEnumerable<string>? requested,
        IReadOnlyCollection<string>? allowedFields,
        out List<string>? fields,
        out IResult? errorResult)
    {
        errorResult = null;
        if (requested is null)
        {
            fields = null;
            return true;
        }

        var allowlist = BuildAllowlist(MergeAllowlist(efConfig?.AllowedSelectProperties, allowedFields));
        var strict = efConfig?.StrictSelectValidation ?? false;
        var viewModelType = ResolveViewModelType(endpoint);
        var normalized = new List<string>();

        foreach (var field in requested.Where(static f => !string.IsNullOrWhiteSpace(f)))
        {
            var trimmed = field.Trim();
            if (allowlist is not null && !allowlist.Contains(trimmed))
            {
                if (strict)
                {
                    errorResult = BuildBadRequest($"Field '{trimmed}' is not allowed.");
                    fields = null;
                    return false;
                }
                continue;
            }

            if (!TryResolvePathType(viewModelType, trimmed, out _))
            {
                if (strict)
                {
                    errorResult = BuildBadRequest($"Field '{trimmed}' does not exist.");
                    fields = null;
                    return false;
                }
                continue;
            }

            normalized.Add(trimmed);
        }

        fields = normalized.Count == 0 ? null : normalized;
        return true;
    }

    private static Expression<Func<TResponse, object?>>[]? BuildIncludeExpressions(List<string>? includes, out bool useStringIncludes)
    {
        useStringIncludes = includes?.Any(static p => p.Contains('.', StringComparison.Ordinal)) == true;
        if (useStringIncludes || includes is null || includes.Count == 0) return null;
        var expressions = KyrolusEFRepositoryBase<TResponse>.ConvertIncludePropertiesToExpressions(includes);
        return expressions?.Length > 0 ? expressions : null;
    }

    private bool TryBuildIncludeGraph(
        EndpointNames endpoint,
        object? includeGraph,
        IReadOnlyCollection<string>? allowedIncludes,
        out IncludeGraph<TResponse>? graph,
        out IResult? errorResult)
    {
        graph = null;
        errorResult = null;
        if (includeGraph is null) return true;

        if (includeGraph is IncludeGraph<TResponse> typedGraph)
        {
            graph = typedGraph;
            return true;
        }

        var paths = ExtractIncludeGraphPaths(includeGraph);
        return TryBuildIncludeGraph(endpoint, paths, allowedIncludes, out graph, out errorResult);
    }

    private bool TryBuildIncludeGraph(
        EndpointNames endpoint,
        IReadOnlyList<string>? paths,
        IReadOnlyCollection<string>? allowedIncludes,
        out IncludeGraph<TResponse>? graph,
        out IResult? errorResult)
    {
        _ = endpoint;
        graph = null;
        errorResult = null;
        if (paths is null || paths.Count == 0) return true;

        var maxDepth = efConfig?.MaxIncludeGraphDepth ?? 0;
        if (maxDepth <= 0)
        {
            errorResult = BuildBadRequest("IncludeGraph is not enabled.");
            return false;
        }

        var allowlist = BuildAllowlist(MergeAllowlist(efConfig?.AllowedIncludeProperties, allowedIncludes));
        var strict = efConfig?.StrictIncludeValidation ?? false;
        var valid = new List<string>();
        foreach (var trimmed in NormalizeIncludeGraphPaths(paths))
        {
            if (!TryValidateIncludeGraphPath(trimmed, allowlist, strict, maxDepth, out var accepted, out errorResult))
            {
                return false;
            }

            if (accepted is not null)
            {
                valid.Add(accepted);
            }
        }

        if (valid.Count == 0) return true;
        graph = KyrolusIncludeGraphBuilder.FromPaths<TResponse>(valid.ToArray());
        return true;
    }

    private static IEnumerable<string> NormalizeIncludeGraphPaths(IReadOnlyList<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;
            yield return path.Trim();
        }
    }

    private bool TryValidateIncludeGraphPath(
        string trimmed,
        ISet<string>? allowlist,
        bool strict,
        int maxDepth,
        out string? accepted,
        out IResult? errorResult)
    {
        accepted = null;
        errorResult = null;

        if (allowlist is not null && !allowlist.Contains(trimmed))
        {
            return TryRejectIncludeGraphPath(strict, $"IncludeGraph '{trimmed}' is not allowed.", out errorResult);
        }

        if (!TryResolvePathType(typeof(TResponse), trimmed, out _))
        {
            return TryRejectIncludeGraphPath(strict, $"IncludeGraph '{trimmed}' does not exist.", out errorResult);
        }

        var depth = trimmed.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
        if (depth > maxDepth)
        {
            return TryRejectIncludeGraphPath(strict, $"IncludeGraph '{trimmed}' exceeds max depth {maxDepth}.", out errorResult);
        }

        accepted = trimmed;
        return true;
    }

    private bool TryRejectIncludeGraphPath(bool strict, string message, out IResult? errorResult)
    {
        if (strict)
        {
            errorResult = BuildBadRequest(message);
            return false;
        }

        errorResult = null;
        return true;
    }

    private static IReadOnlyList<string>? ExtractIncludeGraphPaths(object? includeGraph)
    {
        if (includeGraph is null) return null;
        if (includeGraph is string raw)
        {
            return SplitCsv(raw);
        }

        if (includeGraph is IEnumerable<string> list)
        {
            return [.. list.Where(static p => !string.IsNullOrWhiteSpace(p)).Select(static p => p.Trim())];
        }

        if (includeGraph is JsonElement json)
        {
            return ExtractIncludeGraphPathsFromJson(json);
        }

        return null;
    }

    private static IReadOnlyList<string>? ExtractIncludeGraphPathsFromJson(JsonElement json)
    {
        if (json.ValueKind == JsonValueKind.String)
        {
            return SplitCsv(json.GetString());
        }

        if (json.ValueKind != JsonValueKind.Array) return null;
        var values = new List<string>();
        foreach (var item in json.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var value = item.GetString();
            if (!string.IsNullOrWhiteSpace(value)) values.Add(value);
        }
        return values.Count == 0 ? null : values;
    }

    private (int PageNumber, int PageSize) NormalizePaging(int? pageNumber, int? pageSize)
    {
        var defaultSize = efConfig?.DefaultPageSize > 0 ? efConfig.DefaultPageSize : 50;
        var maxSize = efConfig?.MaxPageSize > 0 ? efConfig.MaxPageSize : 200;
        var size = pageSize ?? defaultSize;
        if (size < 1) size = defaultSize;
        if (size > maxSize) size = maxSize;
        var number = pageNumber ?? 1;
        if (number < 1) number = 1;
        return (number, size);
    }

    private int ResolveBulkChunkSize()
    {
        var chunk = efConfig?.BulkChunkSize ?? 200;
        return chunk < 1 ? 1 : chunk;
    }

    private static async IAsyncEnumerable<List<T>> ChunkAsync<T>(
        IAsyncEnumerable<T> source,
        int chunkSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var buffer = new List<T>(chunkSize);
        await foreach (var item in source.WithCancellation(cancellationToken))
        {
            buffer.Add(item);
            if (buffer.Count >= chunkSize)
            {
                yield return buffer;
                buffer = new List<T>(chunkSize);
            }
        }

        if (buffer.Count > 0)
        {
            yield return buffer;
        }
    }

    private static void ApplyCacheable(object request, bool? cacheable)
    {
        if (cacheable is null) return;
        if (request is ICacheableRequest cacheableRequest)
        {
            cacheableRequest.Cacheable = cacheable.Value;
        }
    }

    private Task SendCommandAsync(IKyrolusCommandBase command, CancellationToken cancellationToken)
    {
        if (command is IKyrolusCommand nonGeneric)
        {
            return mediator.SendAsync(nonGeneric, cancellationToken);
        }

        return mediator.SendAsync((dynamic)command, cancellationToken);
    }

    private static void ApplyGetAllQueryOptions(
        IKyrolusQuery<IEnumerable<TResponse>> query,
        GetAllQueryOptions options)
    {
        if (query is GetAllQuery<TResponse> getAll)
        {
            getAll.Filter = options.Filter;
            getAll.OrderBy = options.OrderBy;
            getAll.IncludeProperties = options.IncludeProperties;
            getAll.IncludeExpressions = options.IncludeExpressions;
            getAll.IncludeGraph = options.IncludeGraph;
            getAll.AsNoTracking = options.AsNoTracking;
            getAll.UseSplitQuery = options.UseSplitQuery;
            return;
        }

        TrySetProperty(query, "Filter", options.Filter);
        TrySetProperty(query, "OrderBy", options.OrderBy);
        TrySetProperty(query, "IncludeProperties", options.IncludeProperties);
        TrySetProperty(query, "IncludeExpressions", options.IncludeExpressions);
        TrySetProperty(query, "IncludeGraph", options.IncludeGraph);
        TrySetProperty(query, "AsNoTracking", options.AsNoTracking);
        TrySetProperty(query, UseSplitQueryPropertyName, options.UseSplitQuery);
    }

    private void ApplyGetByIdQueryOptions(
        IKyrolusQuery<TResponse?> query,
        TKey id,
        List<string>? includeProperties,
        Expression<Func<TResponse, object?>>[]? includeExpressions,
        IncludeGraph<TResponse>? includeGraph,
        bool? asNoTracking,
        bool? useSplitQuery)
    {
        if (query is GetByIdQuery<TResponse, TKey> getById)
        {
            getById.Id = id;
            getById.IncludeProperties = includeProperties;
            getById.IncludeExpressions = includeExpressions;
            getById.IncludeGraph = includeGraph;
            getById.AsNoTracking = asNoTracking;
            getById.UseSplitQuery = useSplitQuery;
            return;
        }

        TrySetProperty(query, "Id", id);
        TrySetProperty(query, "IncludeProperties", includeProperties);
        TrySetProperty(query, "IncludeExpressions", includeExpressions);
        TrySetProperty(query, "IncludeGraph", includeGraph);
        TrySetProperty(query, "AsNoTracking", asNoTracking);
        TrySetProperty(query, UseSplitQueryPropertyName, useSplitQuery);
    }

    private static void TrySetProperty(object target, string propertyName, object? value)
    {
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null || !property.CanWrite) return;

        if (value is null)
        {
            if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null) return;
            property.SetValue(target, null);
            return;
        }

        if (property.PropertyType.IsInstanceOfType(value))
        {
            property.SetValue(target, value);
        }
    }

    private bool TrySetEntityId(TResponse entity, TKey id, out IResult? errorResult)
    {
        errorResult = null;
        if (config.GetEntityId is not null)
        {
            var existing = config.GetEntityId(entity);
            if (existing is TKey existingKey && !EqualityComparer<TKey>.Default.Equals(existingKey, id))
            {
                errorResult = BuildBadRequest("The provided id does not match the entity id.");
                return false;
            }
        }

        if (config.SetEntityId is not null)
        {
            config.SetEntityId(entity, id);
            return true;
        }

        var keyProperty = efConfig?.KeyPropertyName;
        if (!string.IsNullOrWhiteSpace(keyProperty) && !TrySetPropertyValue(entity, keyProperty, id))
        {
            errorResult = BuildBadRequest($"Cannot set key property '{keyProperty}'.");
            return false;

        }

        return true;
    }

    private bool TrySetCompositeKey(TResponse entity, object?[] keyValues, out IResult? errorResult)
    {
        errorResult = null;
        if (efConfig?.SetCompositeKey is not null)
        {
            efConfig.SetCompositeKey(entity, keyValues);
            return true;
        }

        var names = efConfig?.CompositeKeyPropertyNames;
        if (names is null || names.Count == 0)
        {
            return true;
        }

        if (names.Count != keyValues.Length)
        {
            errorResult = BuildBadRequest($"Composite key expects {names.Count} values.");
            return false;
        }

        for (var i = 0; i < names.Count; i++)
        {
            if (!TrySetPropertyValue(entity, names[i], keyValues[i]))
            {
                errorResult = BuildBadRequest($"Cannot set composite key property '{names[i]}'.");
                return false;
            }
        }

        return true;
    }

    private static bool TrySetPropertyValue(object target, string propertyName, object? value)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;
        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        if (property is null || !property.CanWrite) return false;

        if (value is null)
        {
            if (property.PropertyType.IsValueType && Nullable.GetUnderlyingType(property.PropertyType) is null) return false;
            property.SetValue(target, null);
            return true;
        }

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (targetType.IsInstanceOfType(value))
        {
            property.SetValue(target, value);
            return true;
        }

        if (value is string raw && TryConvertKey(raw, targetType, out var converted))
        {
            property.SetValue(target, converted);
            return true;
        }

        try
        {
            var convertedValue = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            property.SetValue(target, convertedValue);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private IEndpointConfig? GetEndpointConfig(EndpointNames endpoint)
        => config.EndpointConfig?.FirstOrDefault(e => e.Name == endpoint);

    private static ISet<string>? BuildAllowlist(IReadOnlyCollection<string>? allowed)
        => allowed is null || allowed.Count == 0 ? null : new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyCollection<string>? MergeAllowlist(
        IReadOnlyCollection<string>? baseAllowed,
        IReadOnlyCollection<string>? extraAllowed)
    {
        if (baseAllowed is null || baseAllowed.Count == 0) return extraAllowed;
        if (extraAllowed is null || extraAllowed.Count == 0) return baseAllowed;
        return baseAllowed.Intersect(extraAllowed, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private bool TryResolveSeekProperties(out IReadOnlyList<string> properties, out IResult? errorResult)
    {
        errorResult = null;
        properties = Array.Empty<string>();
        if (efConfig?.CompositeKeyPropertyNames is { Count: > 0 } composite)
        {
            properties = composite;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(efConfig?.KeyPropertyName))
        {
            properties = [efConfig.KeyPropertyName];
            return true;
        }

        errorResult = BuildBadRequest("Seek properties are not configured.");
        return false;
    }

    private bool TryBuildKeyValues(IReadOnlyList<string>? keys, out object?[] keyValues, out IResult? errorResult)
    {
        errorResult = null;
        var normalized = NormalizeKeys(keys);
        if (normalized.Count == 0)
        {
            errorResult = BuildBadRequest("Composite key is required.");
            keyValues = [];
            return false;
        }

        if (efConfig?.CompositeKeyParser is not null)
        {
            try
            {
                keyValues = efConfig.CompositeKeyParser(normalized);
            }
            catch (Exception ex)
            {
                errorResult = BuildBadRequest(ex.Message);
                keyValues = [];
                return false;
            }

            if (keyValues.Length == 0)
            {
                errorResult = BuildBadRequest("Composite key is required.");
                return false;
            }

            return true;
        }

        var keyTypes = efConfig?.CompositeKeyTypes;
        if (keyTypes is not null && keyTypes.Count > 0)
        {
            if (keyTypes.Count != normalized.Count)
            {
                errorResult = BuildBadRequest($"Composite key expects {keyTypes.Count} values.");
                keyValues = [];
                return false;
            }

            var converted = new object?[keyTypes.Count];
            for (var i = 0; i < keyTypes.Count; i++)
            {
                if (!TryConvertKey(normalized[i], keyTypes[i], out var value))
                {
                    errorResult = BuildBadRequest($"Invalid key value '{normalized[i]}' for type '{keyTypes[i].Name}'.");
                    keyValues = [];
                    return false;
                }

                converted[i] = value;
            }

            keyValues = converted;
            return true;
        }

        keyValues = normalized.Cast<object?>().ToArray();
        return true;
    }

    private static IReadOnlyList<string> NormalizeKeys(IReadOnlyList<string>? keys)
    {
        if (keys is null || keys.Count == 0) return Array.Empty<string>();
        var list = keys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .SelectMany(k => k.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();
        return list;
    }

    private static readonly IReadOnlyDictionary<Type, Func<string, (bool Success, object? Value)>> KnownKeyParsers =
        new Dictionary<Type, Func<string, (bool Success, object? Value)>>
        {
            [typeof(string)] = raw => (true, raw),
            [typeof(Guid)] = raw => Guid.TryParse(raw, out var guid) ? (true, guid) : (false, null),
            [typeof(DateTimeOffset)] = raw =>
                DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto)
                    ? (true, dto)
                    : (false, null),
            [typeof(DateTime)] = raw =>
                DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
                    ? (true, dt)
                    : (false, null),
            [typeof(DateOnly)] = raw =>
                DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly)
                    ? (true, dateOnly)
                    : (false, null),
            [typeof(TimeOnly)] = raw =>
                TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly)
                    ? (true, timeOnly)
                    : (false, null)
        };

    private static bool TryConvertKey(string raw, Type targetType, out object? value)
    {
        var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (KnownKeyParsers.TryGetValue(nonNullable, out var parser))
        {
            var parsed = parser(raw);
            value = parsed.Value;
            return parsed.Success;
        }

        if (nonNullable.IsEnum)
        {
            if (Enum.TryParse(nonNullable, raw, true, out var enumValue))
            {
                value = enumValue;
                return true;
            }
            value = null;
            return false;
        }

        var converter = TypeDescriptor.GetConverter(nonNullable);
        if (converter.CanConvertFrom(typeof(string)))
        {
            try
            {
                value = converter.ConvertFromInvariantString(raw);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        try
        {
            value = Convert.ChangeType(raw, nonNullable, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static object?[] BuildKeyValues(TKey id) => [id];

    private void ApplyGetByKeyValuesQueryOptions(
        IKyrolusQuery<TResponse?> query,
        object?[] keyValues,
        List<string>? includeProperties,
        Expression<Func<TResponse, object?>>[]? includeExpressions,
        IncludeGraph<TResponse>? includeGraph,
        bool? asNoTracking,
        bool? useSplitQuery)
    {
        if (query is GetByKeyValuesQuery<TResponse, TKey> getByKeys)
        {
            getByKeys.KeyValues = keyValues;
            getByKeys.IncludeProperties = includeProperties;
            getByKeys.IncludeExpressions = includeExpressions;
            getByKeys.IncludeGraph = includeGraph;
            getByKeys.AsNoTracking = asNoTracking;
            getByKeys.UseSplitQuery = useSplitQuery;
            return;
        }

        TrySetProperty(query, KeyValuesPropertyName, keyValues);
        TrySetProperty(query, "IncludeProperties", includeProperties);
        TrySetProperty(query, "IncludeExpressions", includeExpressions);
        TrySetProperty(query, "IncludeGraph", includeGraph);
        TrySetProperty(query, "AsNoTracking", asNoTracking);
        TrySetProperty(query, UseSplitQueryPropertyName, useSplitQuery);
    }

    private IResult BuildSuccess(object? data, EndpointNames endpoint, int statusCode, IReadOnlyList<string>? selectedFields = null)
    {
        if (data is null)
        {
            if (!config.UseEnrichedCustomResponse)
            {
                return Results.StatusCode(statusCode);
            }

            var emptyResponse = new Response(statusCode, "Success", true, null);
            return Results.Json(emptyResponse, statusCode: statusCode);
        }

        var mapped = MapData(data, endpoint);
        var shaped = selectedFields is null || selectedFields.Count == 0
            ? mapped
            : ApplyFieldSelection(mapped, selectedFields);
        if (!config.UseEnrichedCustomResponse)
        {
            return Results.Json(shaped, statusCode: statusCode);
        }

        var response = new Response(statusCode, "Success", true, shaped);
        return Results.Json(response, statusCode: statusCode);
    }

    private IResult BuildBadRequest(string message, IReadOnlyList<KyrolusErrorItem>? errors = null)
        => BuildErrorResult(StatusCodes.Status400BadRequest, KyrolusErrorCodes.BadRequest, message, errors);

    private IResult BuildNotFound()
        => BuildErrorResult(StatusCodes.Status404NotFound, KyrolusErrorCodes.NotFound, "Not found");

    private IResult BuildConflict(string message)
        => BuildErrorResult(StatusCodes.Status409Conflict, KyrolusErrorCodes.ConcurrencyConflict, message);

    private IResult BuildForbidden(string? message = null)
        => BuildErrorResult(StatusCodes.Status403Forbidden, KyrolusErrorCodes.Forbidden, message ?? "Forbidden");

    private IResult BuildValidationError(IReadOnlyList<KyrolusValidationFailure> failures)
    {
        var errors = failures
            .Select(f => new KyrolusErrorItem(f.FieldPath ?? f.PropertyName, f.ErrorCode, f.ErrorMessage))
            .ToArray();
        return BuildErrorResult(StatusCodes.Status400BadRequest, KyrolusErrorCodes.Validation, "Validation error", errors);
    }

    private IResult BuildErrorResult(int statusCode, string code, string title, IReadOnlyList<KyrolusErrorItem>? errors = null)
    {
        if (errorWriter is not null && errorContextFactory is not null && HttpContext is not null)
        {
            var envelope = new KyrolusErrorEnvelope(code, title, TraceId: null, Errors: errors);
            var mapping = new KyrolusExceptionMapping(envelope, (HttpStatusCode)statusCode);
            return new KyrolusErrorResult(mapping, errorWriter, errorContextFactory);
        }

        if (!config.UseEnrichedCustomResponse)
        {
            return statusCode switch
            {
                StatusCodes.Status400BadRequest => Results.BadRequest(title),
                StatusCodes.Status404NotFound => Results.NotFound(),
                StatusCodes.Status409Conflict => Results.Conflict(title),
                _ => Results.StatusCode(statusCode)
            };
        }

        var response = new Response(statusCode, title, false, errors);
        return Results.Json(response, statusCode: statusCode);
    }

    private object MapData(object data, EndpointNames endpoint)
    {
        var viewModelType = ResolveViewModelType(endpoint);
        if (viewModelType == typeof(TResponse) || viewModelType == data.GetType())
        {
            return data;
        }

        if (data is KyrolusPagedResult<TResponse> paged)
        {
            return MapPagedResult(paged, viewModelType);
        }

        var targetType = IsEnumerableResult(data)
            ? typeof(IEnumerable<>).MakeGenericType(viewModelType)
            : viewModelType;

        return data.Adapt(data.GetType(), targetType) ?? data;
    }

    private static object ApplyFieldSelection(object data, IReadOnlyList<string> fields)
    {
        if (TryProjectPagedResult(data, fields, out var pagedProjected))
        {
            return pagedProjected;
        }

        if (data is System.Collections.IEnumerable enumerable && data is not string)
        {
            var list = new List<Dictionary<string, object?>>();
            foreach (var item in enumerable)
            {
                if (item is null) continue;
                list.Add(ProjectItem(item, fields));
            }
            return list;
        }

        return ProjectItem(data, fields);
    }

    private static bool TryProjectPagedResult(object data, IReadOnlyList<string> fields, out object projected)
    {
        projected = data;
        var type = data.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(KyrolusPagedResult<>))
        {
            return false;
        }

        var itemsProp = type.GetProperty("Items");
        var totalProp = type.GetProperty("TotalCount");
        var pageProp = type.GetProperty("PageNumber");
        var sizeProp = type.GetProperty("PageSize");
        if (itemsProp is null || totalProp is null || pageProp is null || sizeProp is null) return false;

        if (itemsProp.GetValue(data) is not System.Collections.IEnumerable items) return false;
        var list = new List<Dictionary<string, object?>>();
        foreach (var item in items)
        {
            if (item is null) continue;
            list.Add(ProjectItem(item, fields));
        }

        var total = (int)totalProp.GetValue(data)!;
        var page = (int)pageProp.GetValue(data)!;
        var size = (int)sizeProp.GetValue(data)!;
        var pagedType = typeof(KyrolusPagedResult<>).MakeGenericType(typeof(Dictionary<string, object?>));
        projected = Activator.CreateInstance(pagedType, list, total, page, size) ?? data;
        return true;
    }

    private static Dictionary<string, object?> ProjectItem(object item, IReadOnlyList<string> fields)
    {
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in fields)
        {
            if (TryGetFieldValue(item, field, out var value))
            {
                dict[field] = value;
            }
        }
        return dict;
    }

    private static bool TryGetFieldValue(object item, string field, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(field)) return false;
        var current = item;
        var currentType = item.GetType();
        foreach (var segment in field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var prop = currentType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null) return false;
            var next = prop.GetValue(current);
            if (next is null)
            {
                value = null;
                return true;
            }
            current = next;
            currentType = next.GetType();
        }

        value = current;
        return true;
    }

    private static bool TryResolvePathType(Type rootType, string field, out Type? resultType)
    {
        resultType = rootType;
        foreach (var segment in field.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var prop = resultType.GetProperty(segment, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is null)
            {
                resultType = null;
                return false;
            }
            resultType = prop.PropertyType;
        }

        return true;
    }

    private static bool IsEnumerableResult(object data)
    {
        if (data is string) return false;
        return data is System.Collections.IEnumerable && data.GetType() != typeof(TResponse);
    }

    private static object MapPagedResult(KyrolusPagedResult<TResponse> paged, Type viewModelType)
    {
        if (viewModelType == typeof(TResponse)) return paged;
        var targetItemsType = typeof(List<>).MakeGenericType(viewModelType);
        var mappedItems = paged.Items.Adapt(targetItemsType);
        var pagedType = typeof(KyrolusPagedResult<>).MakeGenericType(viewModelType);
        return Activator.CreateInstance(pagedType, mappedItems, paged.TotalCount, paged.PageNumber, paged.PageSize) ?? paged;
    }

    private Type ResolveViewModelType(EndpointNames endpoint)
    {
        var endpointConfig = GetEndpointConfig(endpoint);
        if (endpointConfig?.ViewModelType is not null)
        {
            return endpointConfig.ViewModelType;
        }

        return config.ViewModelType ?? typeof(TResponse);
    }
}
