using KyrolusSous.CQRS.Abstractions.Models;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule;
using KyrolusSous.EndpointKit.Core.BaseKyrolusModule.Interfaces;
using KyrolusSous.EndpointKit.Core.Batch;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusEfRouteMapper<TResponse, TModel, TKey> : IRouteMapper<TResponse, TModel, TKey>
    where TResponse : class
    where TModel : class
    where TKey : notnull, IEquatable<TKey>
{
    private readonly DefaultRouteMapper<TResponse, TModel, TKey> coreMapper = new();

    public RouteGroupBuilder MapEndpoints(
        IEndpointRouteBuilder app,
        IKyrolusApiConfig<TResponse> config,
        ICommandQueryHandler<TResponse, TModel, TKey> commandQueryHandler)
    {
        var efConfig = config as IKyrolusEfApiConfig<TResponse>;
        var compositeKeyOnly = efConfig?.CompositeKeyOnly == true;
        EndpointNames[] originalAllEndpointsExcept = config.AllEndpointsExcept.ToArray();
        if (compositeKeyOnly)
        {
            var excluded = new HashSet<EndpointNames>(originalAllEndpointsExcept ?? []);
            excluded.Add(EndpointNames.GetById);
            excluded.Add(EndpointNames.Update);
            excluded.Add(EndpointNames.Patch);
            excluded.Add(EndpointNames.Delete);
            config.AllEndpointsExcept = excluded.ToArray();
        }

        var group = coreMapper.MapEndpoints(app, config, commandQueryHandler);
        var endpointsToMap = GetEndpointsToMap(config);
        bool ShouldMap(EndpointNames currentEndpoint) => config.AllEndpointsExcept is not null ?
            !endpointsToMap.Contains(currentEndpoint) : endpointsToMap.Contains(currentEndpoint)
            || endpointsToMap.Contains(EndpointNames.All);

        if (commandQueryHandler is not IKyrolusEfCommandQueryHandler<TResponse, TModel, TKey> efHandler)
        {
            if (compositeKeyOnly)
            {
                config.AllEndpointsExcept = originalAllEndpointsExcept ?? Array.Empty<EndpointNames>();
            }
            return group;
        }

        if (efConfig is { EnableQueryEndpoints: true })
        {
            group.MapPost($"/{config.Route}s/query", efHandler.HandleQueryAsync)
                .Authorize(Authorize(config, EndpointNames.Query))
                .ApplyOpenApi(config, EndpointNames.Query)
                .ApplyEndpointPolicies(config, EndpointNames.Query);
        }

        if (efConfig is { EnableCountEndpoint: true })
        {
            group.MapGet($"/{config.Route}s/$count", efHandler.HandleCountAsync)
                .Authorize(Authorize(config, EndpointNames.Count))
                .ApplyOpenApi(config, EndpointNames.Count, typeof(long))
                .ApplyEndpointPolicies(config, EndpointNames.Count);
        }

        if (efConfig is { EnableHeadEndpoint: true } && ShouldMap(EndpointNames.GetById))
        {
            group.MapMethods($"/{config.Route}/{{id}}", ["HEAD"], efHandler.HandleHeadByIdAsync)
                .Authorize(Authorize(config, EndpointNames.Head))
                .ApplyOpenApi(config, EndpointNames.Head)
                .ApplyEndpointPolicies(config, EndpointNames.Head);
        }

        if (efConfig is { EnablePagedEndpoints: true })
        {
            var pagedResponseType = ResolvePagedResponseType(config, EndpointNames.Paged);
            group.MapGet($"{config.Route}s/paged", efHandler.HandleGetAllPagedAsync)
                .Authorize(Authorize(config, EndpointNames.Paged))
                .ApplyOpenApi(config, EndpointNames.Paged, pagedResponseType)
                .ApplyEndpointPolicies(config, EndpointNames.Paged);
            group.MapPost($"/{config.Route}s/query/paged", efHandler.HandleQueryPagedAsync)
                .Authorize(Authorize(config, EndpointNames.QueryPaged))
                .ApplyOpenApi(config, EndpointNames.QueryPaged, pagedResponseType)
                .ApplyEndpointPolicies(config, EndpointNames.QueryPaged);
        }

        if (efConfig is { EnableSeekEndpoints: true })
        {
            var seekResponseType = typeof(KyrolusSeekResult<>).MakeGenericType(ResolveViewModelType(config, EndpointNames.Seek));
            group.MapGet($"{config.Route}s/seek", efHandler.HandleSeekAsync)
                .Authorize(Authorize(config, EndpointNames.Seek))
                .ApplyOpenApi(config, EndpointNames.Seek, seekResponseType)
                .ApplyEndpointPolicies(config, EndpointNames.Seek);
            group.MapPost($"/{config.Route}s/query/seek", efHandler.HandleQuerySeekAsync)
                .Authorize(Authorize(config, EndpointNames.QuerySeek))
                .ApplyOpenApi(config, EndpointNames.QuerySeek, seekResponseType)
                .ApplyEndpointPolicies(config, EndpointNames.QuerySeek);
        }

        if (efConfig is { EnableCompositeKeyEndpoints: true } && (compositeKeyOnly || ShouldMap(EndpointNames.GetById)))
        {
            group.MapGet($"/{config.Route}/by-keys", efHandler.HandleGetByKeysAsync)
                .Authorize(Authorize(config, EndpointNames.GetById))
                .ApplyOpenApi(config, EndpointNames.GetById)
                .ApplyEndpointPolicies(config, EndpointNames.GetById);
        }

        if (efConfig is { EnableCompositeKeyEndpoints: true } && (compositeKeyOnly || ShouldMap(EndpointNames.Update)))
        {
            group.MapPut($"/{config.Route}/by-keys", efHandler.HandleUpdateByKeysAsync)
                .Authorize(Authorize(config, EndpointNames.Update))
                .ApplyOpenApi(config, EndpointNames.Update)
                .ApplyEndpointPolicies(config, EndpointNames.Update);
        }

        if (efConfig is { EnableCompositeKeyEndpoints: true } && (compositeKeyOnly || ShouldMap(EndpointNames.Delete)))
        {
            group.MapDelete($"/{config.Route}/by-keys", efHandler.HandleRemoveByKeysAsync)
                .Authorize(Authorize(config, EndpointNames.Delete))
                .ApplyOpenApi(config, EndpointNames.Delete)
                .ApplyEndpointPolicies(config, EndpointNames.Delete);
        }

        if (efConfig is { EnableCompositeKeyEndpoints: true } && (compositeKeyOnly || ShouldMap(EndpointNames.Patch)))
        {
            group.MapPatch($"/{config.Route}/by-keys", efHandler.HandlePatchByKeysAsync)
                .Authorize(Authorize(config, EndpointNames.Patch))
                .ApplyOpenApi(config, EndpointNames.Patch)
                .ApplyEndpointPolicies(config, EndpointNames.Patch);
        }

        if (efConfig is { EnableBulkEndpoints: true } && ShouldMap(EndpointNames.BulkUpdate))
        {
            group.MapPost($"/{config.Route}s/bulk/update", efHandler.HandleBulkUpdateAsync)
                .Authorize(Authorize(config, EndpointNames.BulkUpdate))
                .ApplyOpenApi(config, EndpointNames.BulkUpdate)
                .ApplyEndpointPolicies(config, EndpointNames.BulkUpdate);
        }

        if (efConfig is { EnableBulkEndpoints: true } && ShouldMap(EndpointNames.BulkDelete))
        {
            group.MapPost($"/{config.Route}s/bulk/delete", efHandler.HandleBulkDeleteAsync)
                .Authorize(Authorize(config, EndpointNames.BulkDelete))
                .ApplyOpenApi(config, EndpointNames.BulkDelete)
                .ApplyEndpointPolicies(config, EndpointNames.BulkDelete);
        }

        if (efConfig is { EnableBulkEndpoints: true } && ShouldMap(EndpointNames.BulkUpsert))
        {
            group.MapPost($"/{config.Route}s/bulk/upsert", efHandler.HandleBulkUpsertAsync)
                .Authorize(Authorize(config, EndpointNames.BulkUpsert))
                .ApplyOpenApi(config, EndpointNames.BulkUpsert)
                .ApplyEndpointPolicies(config, EndpointNames.BulkUpsert);
        }

        if (efConfig is { EnableBulkEndpoints: true } && ShouldMap(EndpointNames.BulkPatch))
        {
            group.MapPost($"/{config.Route}s/bulk/patch", efHandler.HandleBulkPatchAsync)
                .Authorize(Authorize(config, EndpointNames.BulkPatch))
                .ApplyOpenApi(config, EndpointNames.BulkPatch)
                .ApplyEndpointPolicies(config, EndpointNames.BulkPatch);
        }

        if (efConfig is { EnableSoftDeleteEndpoints: true } && ShouldMap(EndpointNames.GetDeleted))
        {
            group.MapGet($"/{config.Route}s/deleted", efHandler.HandleGetDeletedAsync)
                .Authorize(Authorize(config, EndpointNames.GetDeleted))
                .ApplyOpenApi(config, EndpointNames.GetDeleted)
                .ApplyEndpointPolicies(config, EndpointNames.GetDeleted);
        }

        if (efConfig is { EnableSoftDeleteEndpoints: true } && ShouldMap(EndpointNames.Restore))
        {
            group.MapPost($"/{config.Route}/{{id}}/restore", efHandler.HandleRestoreAsync)
                .Authorize(Authorize(config, EndpointNames.Restore))
                .ApplyOpenApi(config, EndpointNames.Restore)
                .ApplyEndpointPolicies(config, EndpointNames.Restore);
        }

        if (efConfig is { EnableSoftDeleteEndpoints: true, EnableCompositeKeyEndpoints: true } && (compositeKeyOnly || ShouldMap(EndpointNames.Restore)))
        {
            group.MapPost($"/{config.Route}/by-keys/restore", efHandler.HandleRestoreByKeysAsync)
                .Authorize(Authorize(config, EndpointNames.Restore))
                .ApplyOpenApi(config, EndpointNames.Restore)
                .ApplyEndpointPolicies(config, EndpointNames.Restore);
        }

        if (efConfig is { BatchOptions.Enabled: true })
        {
            var batchResponseType = typeof(KyrolusBatchResponse<,>).MakeGenericType(ResolveViewModelType(config, EndpointNames.Batch), typeof(TKey));
            group.MapPost($"/{config.Route}s/{efConfig.BatchOptions.RouteSuffix}", efHandler.HandleBatchAsync)
                .Authorize(Authorize(config, EndpointNames.Batch))
                .ApplyOpenApi(config, EndpointNames.Batch, batchResponseType)
                .ApplyEndpointPolicies(config, EndpointNames.Batch);
        }

        if (compositeKeyOnly)
        {
            config.AllEndpointsExcept = originalAllEndpointsExcept ?? Array.Empty<EndpointNames>();
        }
        return group;
    }

    private static (bool requireAuthorization, string? policy) Authorize(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
        => (RequireAuthorzation(config, endpoint), GetPolicy(config, endpoint));

    private static bool RequireAuthorzation(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.Authorize;

        return config.AuthorizeAllEndpoints;
    }

    private static string? GetPolicy(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);

        if (endpointConfig is not null)
            return endpointConfig.AuthorizationPolicy;

        return config.GeneralAuthorizationPolicy;
    }

    private static Type ResolvePagedResponseType(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        var viewModelType = endpointConfig?.ViewModelType ?? config.ViewModelType ?? typeof(TResponse);
        return typeof(KyrolusPagedResult<>).MakeGenericType(viewModelType);
    }

    private static Type ResolveViewModelType(IKyrolusApiConfig<TResponse> config, EndpointNames endpoint)
    {
        var endpointConfig = config.EndpointConfig.FirstOrDefault(e => e.Name == endpoint);
        return endpointConfig?.ViewModelType ?? config.ViewModelType ?? typeof(TResponse);
    }

    private static IEnumerable<EndpointNames> GetEndpointsToMap(IKyrolusApiConfig<TResponse> config)
    {
        if (config.AllEndpointsExcept is not null && config.AllEndpointsExcept.Any())
            return config.AllEndpointsExcept.Where(e => e != EndpointNames.All);
        else if (config.Endpoints != null && !config.Endpoints.Contains(EndpointNames.All))
            return config.Endpoints.Count() == 1 ? config.Endpoints : config.Endpoints.Where(e => e != EndpointNames.All);
        else
            return config.Endpoints ?? [];
    }
}
