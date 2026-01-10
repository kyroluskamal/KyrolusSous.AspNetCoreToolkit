using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Helpers;
using System.ComponentModel;
using System.Globalization;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class DefaultCommandQueryHandler<TResponse, TModel, TKey>(
    IKyrolusMapper mapper,
    IKyrolusMediatorSender mediator,
    IKyrolusApiConfig<TResponse> config)
    : ICommandQueryHandler<TResponse, TModel, TKey>,
      IKyrolusEfCommandQueryHandler<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private readonly IKyrolusMapper mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    private readonly IKyrolusMediatorSender mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
    private readonly IKyrolusApiConfig<TResponse> config = config ?? throw new ArgumentNullException(nameof(config));
    private readonly IKyrolusEfApiConfig<TResponse>? efConfig = config as IKyrolusEfApiConfig<TResponse>;

    public async Task<IResult> HandleGetAllAsync(string? filter = null, string? includedProps = null, string? fields = null, bool? cacheable = null)
    {
        if (!TryBuildFilter(filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.GetAll, SplitCsv(includedProps), out var includes, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetAll, SplitCsv(fields), out var selectedFields, out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryAll;
        ApplyCacheable(query, cacheable);
        ApplyGetAllQueryOptions(query, filterExpr, orderBy: null, useStringIncludes ? includes : null, includeExpressions, asNoTracking: null, useSplitQuery: null);

        var result = await mediator.SendAsync(query, CancellationToken.None);
        return BuildSuccess(result ?? Array.Empty<TResponse>(), EndpointNames.GetAll, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleGetByIdAsync(TKey id, string? includedProps = null, string? fields = null, bool? cacheable = null)
    {
        if (!TryBuildIncludes(EndpointNames.GetById, SplitCsv(includedProps), out var includes, out var errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetById, SplitCsv(fields), out var selectedFields, out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryById;
        ApplyCacheable(query, cacheable);
        ApplyGetByIdQueryOptions(query, id, useStringIncludes ? includes : null, includeExpressions, asNoTracking: null, useSplitQuery: null);

        var result = await mediator.SendAsync(query, CancellationToken.None);
        if (result is null) return BuildNotFound();
        return BuildSuccess(result, EndpointNames.GetById, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleCreateAsync(TModel model, bool? cacheable = null)
    {
        var entity = mapper.MapModelToEntity<TModel, TResponse>(model);
        var command = config.AddCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.Add, StatusCodes.Status201Created);
    }

    public async Task<IResult> HandleCreateRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var entities = mapper.MapModelToEntity<TModel, TResponse>(model);
        var command = config.AddRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.AddRange, StatusCodes.Status201Created);
    }

    public async Task<IResult> HandleUpdateAsync(TKey id, TModel model, bool? cacheable = null)
    {
        var entity = mapper.MapModelToEntity<TModel, TResponse>(model);
        if (!TrySetEntityId(entity, id, out var errorResult)) return errorResult!;

        var command = config.UpdateCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.Update, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleUpdateRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var entities = mapper.MapModelToEntity<TModel, TResponse>(model);
        var command = config.UpdateRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.UpdateRange, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveAsync(TKey id, bool? cacheable = null)
    {
        var keyValues = BuildKeyValues(id);
        var command = config.RemoveCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "KeyValues", keyValues);

        await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(true, EndpointNames.Delete, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveRangeAsync(IEnumerable<TModel> model, bool? cacheable = null)
    {
        var entities = mapper.MapModelToEntity<TModel, TResponse>(model);
        var command = config.RemoveRangeCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entities", entities);

        await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(true, EndpointNames.DeleteRange, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandlePatchAsync(TKey id, Dictionary<string, object> updates, bool? cacheable = null)
    {
        var keyValues = BuildKeyValues(id);
        var command = config.PatchCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "KeyValues", keyValues);
        TrySetProperty(command, "Updates", updates);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.Patch, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleGetByKeysAsync(string[]? keys, string? includedProps = null, string? fields = null, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.GetById, SplitCsv(includedProps), out var includes, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.GetById, SplitCsv(fields), out var selectedFields, out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = efConfig?.QueryByKeyValues ?? new GetByKeyValuesQuery<TResponse, TKey>(keyValues, cacheable ?? false);

        ApplyCacheable(query, cacheable);
        ApplyGetByKeyValuesQueryOptions(query, keyValues, useStringIncludes ? includes : null, includeExpressions, asNoTracking: null, useSplitQuery: null);

        var result = await mediator.SendAsync(query, CancellationToken.None);
        if (result is null) return BuildNotFound();
        return BuildSuccess(result, EndpointNames.GetById, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleUpdateByKeysAsync(string[]? keys, TModel model, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;

        var entity = mapper.MapModelToEntity<TModel, TResponse>(model);
        if (!TrySetCompositeKey(entity, keyValues, out errorResult)) return errorResult!;

        var command = config.UpdateCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "Entity", entity);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.Update, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleRemoveByKeysAsync(string[]? keys, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;

        var command = config.RemoveCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "KeyValues", keyValues);

        await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(true, EndpointNames.Delete, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandlePatchByKeysAsync(string[]? keys, Dictionary<string, object> updates, bool? cacheable = null)
    {
        if (!TryBuildKeyValues(keys, out var keyValues, out var errorResult)) return errorResult!;

        var command = config.PatchCommand;
        ApplyCacheable(command, cacheable);
        TrySetProperty(command, "KeyValues", keyValues);
        TrySetProperty(command, "Updates", updates);

        var result = await mediator.SendAsync(command, CancellationToken.None);
        return BuildSuccess(result, EndpointNames.Patch, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleQueryAsync(QueryRequest? request, bool? cacheable = null, CancellationToken cancellationToken = default)
    {
        request ??= new QueryRequest();
        if (!TryBuildFilter(request.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(request.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.Query, request.Includes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.Query, request.Fields, out var selectedFields, out errorResult)) return errorResult!;

        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        var query = config.QueryByProperty;
        ApplyCacheable(query, cacheable);
        ApplyGetAllQueryOptions(query, filterExpr, orderExpr, useStringIncludes ? includes : null, includeExpressions, request.AsNoTracking, request.UseSplitQuery);

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result ?? Array.Empty<TResponse>(), EndpointNames.Query, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleGetAllPagedAsync(KyrolusEfQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        if (!TryBuildFilter(parameters.Filter, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(parameters.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.Paged, SplitCsv(parameters.Includes), out var includes, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.Paged, SplitCsv(parameters.Fields), out var selectedFields, out errorResult)) return errorResult!;

        var (pageNumber, pageSize) = NormalizePaging(parameters.PageNumber, parameters.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        if (useStringIncludes)
        {
            var options = new GetAllPagedOptions(
                filterExpr,
                orderExpr,
                includes,
                parameters.AsNoTracking,
                parameters.UseSplitQuery,
                pageNumber,
                pageSize,
                parameters.Cacheable);
            var paged = await BuildPagedFromGetAll(options, cancellationToken);
            return BuildSuccess(paged, EndpointNames.Paged, StatusCodes.Status200OK, selectedFields);
        }

        var query = new GetPagedQuery<TResponse, TKey>(pageNumber, pageSize, parameters.Cacheable ?? false)
        {
            Filter = filterExpr,
            OrderBy = orderExpr,
            IncludeExpressions = includeExpressions,
            AsNoTracking = parameters.AsNoTracking,
            UseSplitQuery = parameters.UseSplitQuery
        };

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.Paged, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleQueryPagedAsync(KyrolusEfPagedQueryRequest request, CancellationToken cancellationToken = default)
    {
        var queryRequest = request.Request ?? new QueryRequest();
        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;
        if (!TryBuildOrder(queryRequest.OrderBy, out var orderExpr, out errorResult)) return errorResult!;
        if (!TryBuildIncludes(EndpointNames.QueryPaged, queryRequest.Includes, out var includes, out errorResult)) return errorResult!;
        if (!TryBuildFields(EndpointNames.QueryPaged, queryRequest.Fields, out var selectedFields, out errorResult)) return errorResult!;

        var (pageNumber, pageSize) = NormalizePaging(request.PageNumber, request.PageSize);
        var includeExpressions = BuildIncludeExpressions(includes, out var useStringIncludes);
        if (useStringIncludes)
        {
            var options = new GetAllPagedOptions(
                filterExpr,
                orderExpr,
                includes,
                queryRequest.AsNoTracking,
                queryRequest.UseSplitQuery,
                pageNumber,
                pageSize,
                request.Cacheable);
            var paged = await BuildPagedFromGetAll(options, cancellationToken);
            return BuildSuccess(paged, EndpointNames.QueryPaged, StatusCodes.Status200OK, selectedFields);
        }

        var query = new GetPagedQuery<TResponse, TKey>(pageNumber, pageSize, request.Cacheable ?? false)
        {
            Filter = filterExpr,
            OrderBy = orderExpr,
            IncludeExpressions = includeExpressions,
            AsNoTracking = queryRequest.AsNoTracking,
            UseSplitQuery = queryRequest.UseSplitQuery
        };

        var result = await mediator.SendAsync(query, cancellationToken);
        return BuildSuccess(result, EndpointNames.QueryPaged, StatusCodes.Status200OK, selectedFields);
    }

    public async Task<IResult> HandleBulkUpdateAsync(KyrolusEfBulkUpdateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Updates is null || request.Updates.Count == 0)
        {
            return BuildBadRequest("Updates are required.");
        }

        var queryRequest = request.Request ?? new QueryRequest();
        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;

        var command = efConfig?.ExecuteUpdateCommand
            ?? new ExecuteUpdateCommand<TResponse, TKey>(filterExpr, request.Updates, request.Cacheable ?? false, queryRequest.UseSplitQuery);

        ApplyCacheable(command, request.Cacheable);
        TrySetProperty(command, "Filter", filterExpr);
        TrySetProperty(command, "Updates", request.Updates);
        TrySetProperty(command, "UseSplitQuery", queryRequest.UseSplitQuery);

        var result = await mediator.SendAsync(command, cancellationToken);
        return BuildSuccess(result, EndpointNames.BulkUpdate, StatusCodes.Status200OK);
    }

    public async Task<IResult> HandleBulkDeleteAsync(KyrolusEfBulkDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var queryRequest = request.Request ?? new QueryRequest();
        if (!TryBuildFilter(queryRequest.Filters, out var filterExpr, out var errorResult)) return errorResult!;

        var command = efConfig?.ExecuteDeleteCommand
            ?? new ExecuteDeleteCommand<TResponse, TKey>(filterExpr, request.Cacheable ?? false, queryRequest.UseSplitQuery);

        ApplyCacheable(command, request.Cacheable);
        TrySetProperty(command, "Filter", filterExpr);
        TrySetProperty(command, "UseSplitQuery", queryRequest.UseSplitQuery);

        var result = await mediator.SendAsync(command, cancellationToken);
        return BuildSuccess(result, EndpointNames.BulkDelete, StatusCodes.Status200OK);
    }

    private sealed record GetAllPagedOptions(
        Expression<Func<TResponse, bool>>? Filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? OrderBy,
        List<string>? IncludeProperties,
        bool? AsNoTracking,
        bool? UseSplitQuery,
        int PageNumber,
        int PageSize,
        bool? Cacheable);

    private async Task<KyrolusPagedResult<TResponse>> BuildPagedFromGetAll(
        GetAllPagedOptions options,
        CancellationToken cancellationToken)
    {
        var getAllQuery = new GetAllQuery<TResponse>(options.Cacheable ?? false)
        {
            Filter = options.Filter,
            OrderBy = options.OrderBy,
            IncludeProperties = options.IncludeProperties,
            AsNoTracking = options.AsNoTracking,
            UseSplitQuery = options.UseSplitQuery
        };

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

    private bool TryBuildFilter(string? filter, out Expression<Func<TResponse, bool>>? expression, out IResult? errorResult)
    {
        errorResult = null;
        var strict = efConfig?.StrictFilterValidation ?? false;
        if (!FilterBuilder.TryBuildFilterExpression<TResponse>(filter, BuildAllowlist(efConfig?.AllowedFilterProperties), strict, out expression, out var error))
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
        if (!FilterBuilder.TryBuildFilterExpression<TResponse>(clauses, BuildAllowlist(efConfig?.AllowedFilterProperties), strict, out expression, out var error))
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

    private bool TryBuildIncludes(EndpointNames endpoint, IEnumerable<string>? requested, out List<string>? includes, out IResult? errorResult)
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
        includes = KyrolusSousRoutingHelpers.GetIncludedProperties(
            merged,
            BuildAllowlist(efConfig?.AllowedIncludeProperties),
            strict,
            out var error);

        if (error is null) return true;

        errorResult = BuildBadRequest(error);
        return false;
    }

    private bool TryBuildFields(EndpointNames endpoint, IEnumerable<string>? requested, out List<string>? fields, out IResult? errorResult)
    {
        errorResult = null;
        if (requested is null)
        {
            fields = null;
            return true;
        }

        var allowlist = BuildAllowlist(efConfig?.AllowedSelectProperties);
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

    private static void ApplyCacheable(object request, bool? cacheable)
    {
        if (cacheable is null) return;
        if (request is ICacheableRequest cacheableRequest)
        {
            cacheableRequest.Cacheable = cacheable.Value;
        }
    }

    private void ApplyGetAllQueryOptions(
        IKyrolusQuery<IEnumerable<TResponse>> query,
        Expression<Func<TResponse, bool>>? filter,
        Func<IQueryable<TResponse>, IOrderedQueryable<TResponse>>? orderBy,
        List<string>? includeProperties,
        Expression<Func<TResponse, object?>>[]? includeExpressions,
        bool? asNoTracking,
        bool? useSplitQuery)
    {
        if (query is GetAllQuery<TResponse> getAll)
        {
            getAll.Filter = filter;
            getAll.OrderBy = orderBy;
            getAll.IncludeProperties = includeProperties;
            getAll.IncludeExpressions = includeExpressions;
            getAll.AsNoTracking = asNoTracking;
            getAll.UseSplitQuery = useSplitQuery;
            return;
        }

        TrySetProperty(query, "Filter", filter);
        TrySetProperty(query, "OrderBy", orderBy);
        TrySetProperty(query, "IncludeProperties", includeProperties);
        TrySetProperty(query, "IncludeExpressions", includeExpressions);
        TrySetProperty(query, "AsNoTracking", asNoTracking);
        TrySetProperty(query, "UseSplitQuery", useSplitQuery);
    }

    private void ApplyGetByIdQueryOptions(
        IKyrolusQuery<TResponse?> query,
        TKey id,
        List<string>? includeProperties,
        Expression<Func<TResponse, object?>>[]? includeExpressions,
        bool? asNoTracking,
        bool? useSplitQuery)
    {
        if (query is GetByIdQuery<TResponse, TKey> getById)
        {
            getById.Id = id;
            getById.IncludeProperties = includeProperties;
            getById.IncludeExpressions = includeExpressions;
            getById.AsNoTracking = asNoTracking;
            getById.UseSplitQuery = useSplitQuery;
            return;
        }

        TrySetProperty(query, "Id", id);
        TrySetProperty(query, "IncludeProperties", includeProperties);
        TrySetProperty(query, "IncludeExpressions", includeExpressions);
        TrySetProperty(query, "AsNoTracking", asNoTracking);
        TrySetProperty(query, "UseSplitQuery", useSplitQuery);
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
        if (!string.IsNullOrWhiteSpace(keyProperty))
        {
            if (!TrySetPropertyValue(entity, keyProperty, id))
            {
                errorResult = BuildBadRequest($"Cannot set key property '{keyProperty}'.");
                return false;
            }
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
            var converted = Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            property.SetValue(target, converted);
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
        var list = new List<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key)) continue;
            foreach (var part in key.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!string.IsNullOrWhiteSpace(part))
                {
                    list.Add(part);
                }
            }
        }

        return list;
    }

    private static bool TryConvertKey(string raw, Type targetType, out object? value)
    {
        var nonNullable = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (nonNullable == typeof(string))
        {
            value = raw;
            return true;
        }

        if (nonNullable == typeof(Guid))
        {
            if (Guid.TryParse(raw, out var guid))
            {
                value = guid;
                return true;
            }
            value = null;
            return false;
        }

        if (nonNullable == typeof(DateTimeOffset))
        {
            if (DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dto))
            {
                value = dto;
                return true;
            }
            value = null;
            return false;
        }

        if (nonNullable == typeof(DateTime))
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt))
            {
                value = dt;
                return true;
            }
            value = null;
            return false;
        }

        if (nonNullable == typeof(DateOnly))
        {
            if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateOnly))
            {
                value = dateOnly;
                return true;
            }
            value = null;
            return false;
        }

        if (nonNullable == typeof(TimeOnly))
        {
            if (TimeOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeOnly))
            {
                value = timeOnly;
                return true;
            }
            value = null;
            return false;
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
        bool? asNoTracking,
        bool? useSplitQuery)
    {
        if (query is GetByKeyValuesQuery<TResponse, TKey> getByKeys)
        {
            getByKeys.KeyValues = keyValues;
            getByKeys.IncludeProperties = includeProperties;
            getByKeys.IncludeExpressions = includeExpressions;
            getByKeys.AsNoTracking = asNoTracking;
            getByKeys.UseSplitQuery = useSplitQuery;
            return;
        }

        TrySetProperty(query, "KeyValues", keyValues);
        TrySetProperty(query, "IncludeProperties", includeProperties);
        TrySetProperty(query, "IncludeExpressions", includeExpressions);
        TrySetProperty(query, "AsNoTracking", asNoTracking);
        TrySetProperty(query, "UseSplitQuery", useSplitQuery);
    }

    private IResult BuildSuccess(object data, EndpointNames endpoint, int statusCode, IReadOnlyList<string>? selectedFields = null)
    {
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

    private IResult BuildBadRequest(string message)
    {
        if (!config.UseEnrichedCustomResponse)
        {
            return Results.BadRequest(message);
        }

        var response = new Response(StatusCodes.Status400BadRequest, message, false);
        return Results.Json(response, statusCode: StatusCodes.Status400BadRequest);
    }

    private IResult BuildNotFound()
    {
        if (!config.UseEnrichedCustomResponse)
        {
            return Results.NotFound();
        }

        var response = new Response(StatusCodes.Status404NotFound, "Not found", false);
        return Results.Json(response, statusCode: StatusCodes.Status404NotFound);
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



