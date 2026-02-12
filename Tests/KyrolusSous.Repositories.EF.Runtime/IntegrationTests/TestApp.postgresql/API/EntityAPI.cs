namespace KyrolusSous.Repositories.EF.Runtime.TestApp.API;

public static class EntityApi
{
#pragma warning disable S1144 
#pragma warning disable S3776
    extension(IEndpointRouteBuilder app)
    {
        public IEndpointRouteBuilder MapEntity<TEntity, TRepo, TKey, TRepoKey>()
            where TEntity : class
            where TKey : IEquatable<TKey>
            where TRepo : class, IKyrolusRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey>
        {
            RouteGroupBuilder group = app.MapGroup($"api/{typeof(TEntity).Name.ToLowerInvariant()}").WithTags(typeof(TEntity).Name);

            var isComposite = typeof(TRepoKey) == typeof(object) || typeof(TRepoKey) == typeof(object?[]);

            group.MapGet("/", async (
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromQuery] QueryRequest? request,
                CancellationToken ct) =>
            {
                var parts = helper.Build(request ?? new QueryRequest());
                var includeProperties = request?.Includes is { Length: > 0 }
                    ? new List<string>(request.Includes)
                    : null;
                IEnumerable<TEntity> items;
                var repo = Repo(uow);
                if (request is { IncludeDeleted: true } && repo is IKyrolusSoftDeleteRepository<TEntity> x)
                {
                    items = await x.GetAllIncludingDeletedAsync(
                     parts.Filter,
                     parts.OrderBy,
                     includeProperties,
                     parts.IncludeGraph,
                     asNoTracking: parts.AsNoTracking,
                     useSplitQuery: parts.UseSplitQuery,
                     cancellationToken: ct);
                }
                else
                {
                    items = await repo.GetAllAsync(
                    parts.Filter,
                    parts.OrderBy,
                    includeProperties,
                    parts.IncludeGraph,
                    asNoTracking: parts.AsNoTracking,
                    useSplitQuery: parts.UseSplitQuery,
                    cancellationToken: ct);
                }
                return Results.Ok(items);
            });
#pragma warning disable S1192 // RequiresUnreferencedCode
            group.MapGet("/{id}", async (
                TKey id,
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromQuery] QueryRequest? request,
                CancellationToken ct) =>
            {
                var repo = Repo(uow);
                if (repo is not IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey> single)
                    return Results.BadRequest("Single-key endpoint requires single-key repo.");

                var parts = helper.Build(request ?? new QueryRequest());
                var includeProperties = request?.Includes is { Length: > 0 }
                    ? new List<string>(request.Includes)
                    : null;
                var entity = await single.GetByIdAsync(
                    id,
                    includeProperties,
                    parts.IncludeGraph,
                    asNoTracking: parts.AsNoTracking,
                    useSplitQuery: parts.UseSplitQuery,
                    cancellationToken: ct);
                return entity is not null ? Results.Ok(entity) : Results.NotFound();
            });

            // Alternate route without path id for composite keys via query only
            group.MapGet("by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromQuery] QueryRequest? request,
                CancellationToken ct) =>
            {
                if (!isComposite && keys.Length == 1)
                {
                    // allow single-key usage if user prefers query param style
                    if (Repo(uow) is not IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey>  repoSingle ) return Results.BadRequest("Single-key endpoint requires single-key repo.");
                    var partsSingle = helper.Build(request ?? new QueryRequest());
                    var includePropertiesSingle = request?.Includes is { Length: > 0 }
                        ? new List<string>(request.Includes)
                        : null;
                    var single = await repoSingle.GetByIdAsync(
                        CastKey(ParseObject(keys[0])),
                        includePropertiesSingle,
                        partsSingle.IncludeGraph,
                        asNoTracking: partsSingle.AsNoTracking,
                        useSplitQuery: partsSingle.UseSplitQuery,
                        cancellationToken: ct);
                    return single is not null ? Results.Ok(single) : Results.NotFound();
                }

                if (Repo(uow) is not IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey> repoComposite) return Results.BadRequest("Composite-key endpoint requires composite-key repo.");

                var parts = helper.Build(request ?? new QueryRequest());
                var includeProperties = request?.Includes is { Length: > 0 }
                    ? new List<string>(request.Includes)
                    : null;
                var keyValues = ResolveCompositeKeys(keys);
                var entity = await repoComposite.GetByIdAsync(
                    keyValues,
                    includeProperties,
                    parts.IncludeGraph,
                    asNoTracking: parts.AsNoTracking,
                    useSplitQuery: parts.UseSplitQuery,
                    cancellationToken: ct);

                return entity is not null ? Results.Ok(entity) : Results.NotFound();
            });

            group.MapPost("/", async (
                TEntity entity,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                await Repo(uow).AddAsync(entity, ct);
                await uow.SaveChangesAsync(ct);
                return Results.Created($"/api/{typeof(TEntity).Name.ToLowerInvariant()}", entity);
            });

            group.MapPost("/add-range", async (
                List<TEntity> entities,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                await Repo(uow).AddRangeAsync(entities, ct);
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapPut("/{id?}", async (
                string? id,
                [FromQuery] string[]? keys,
                TEntity entity,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                await Repo(uow).UpdateAsync(entity, ct);
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapPut("/update-range", async (
                IEnumerable<TEntity> entities,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                await Repo(uow).UpdateRangeAsync(entities, ct);
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapDelete("/{id}", async (
                string id,
                IKyrolusUnitOfWork uow,
                [FromQuery] bool softDelete,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /by-id with keys.");

                if (Repo(uow) is not IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey> repo) return Results.BadRequest("Single-key endpoint requires single-key repo.");

                var result = await repo.TryRemoveAsync(CastKey(ParseObject(id)), ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapDelete("/by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                [FromQuery] bool softDelete,
                CancellationToken ct) =>
            {
                var repo = Repo(uow) as IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey>;
                if (repo is null) return Results.BadRequest("Composite-key endpoint requires composite-key repo.");
                var keyValues = ResolveCompositeKeys(keys);

                var result = await repo.TryRemoveAsync(keyValues, ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapDelete("/remove-range", async (
                IEnumerable<TEntity> entities,
                IKyrolusUnitOfWork uow,
                [FromQuery] bool softDelete,
                CancellationToken ct) =>
            {
                await Repo(uow).RemoveRangeAsync(entities, ct);
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapDelete("/{id}/try", async (
                string id,
                IKyrolusUnitOfWork uow,
                [FromQuery] bool softDelete,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /try/by-id with keys.");

                var repo = Repo(uow) as IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey>;
                if (repo is null) return Results.BadRequest("Single-key endpoint requires single-key repo.");

                var result = await repo.TryRemoveAsync(CastKey(ParseObject(id)), ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapDelete("/try/by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                [FromQuery] bool softDelete,
                CancellationToken ct) =>
            {
                var repo = Repo(uow) as IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey>;
                if (repo is null) return Results.BadRequest("Composite-key endpoint requires composite-key repo.");
                var keyValues = ResolveCompositeKeys(keys);
                var result = await repo.TryRemoveAsync(keyValues, ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.NoContent();
            });

            group.MapPatch("/{id}", async (
                string id,
                Dictionary<string, object> updates,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /patch/by-id with keys.");

                var repo = Repo(uow) as IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey>;
                if (repo is null) return Results.BadRequest("Single-key endpoint requires single-key repo.");

                var updatedEntity = await repo.PatchAsync(
                    CastKey(ParseObject(id)),
                    updates,
                    ct);

                if (updatedEntity is null)
                {
                    return Results.NotFound();
                }

                await uow.SaveChangesAsync(ct);
                return Results.Ok(updatedEntity);
            });

            group.MapPatch("/{id}/try", async (
                string id,
                Dictionary<string, object> updates,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /patch/by-id with keys.");

                var repo = Repo(uow) as IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey>;
                if (repo is null) return Results.BadRequest("Single-key endpoint requires single-key repo.");

                var result = await repo.TryPatchAsync(CastKey(ParseObject(id)), updates, ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.Ok(result.Value);
            });

            group.MapPatch("/by-id", async (
                [FromQuery] string[] keys,
                Dictionary<string, object> updates,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                var repo = Repo(uow) as IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey>;
                if (repo is null) return Results.BadRequest("Composite-key endpoint requires composite-key repo.");
                var keyValues = ResolveCompositeKeys(keys);
                var updatedEntity = await repo.PatchAsync(
                    keyValues,
                    updates,
                    ct);

                if (updatedEntity is null)
                {
                    return Results.NotFound();
                }

                await uow.SaveChangesAsync(ct);
                return Results.Ok(updatedEntity);
            });

            group.MapPatch("/try/by-id", async (
                [FromQuery] string[] keys,
                Dictionary<string, object> updates,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                var repo = Repo(uow) as IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey>;
                if (repo is null) return Results.BadRequest("Composite-key endpoint requires composite-key repo.");
                var keyValues = ResolveCompositeKeys(keys);
                var result = await repo.TryPatchAsync(keyValues, updates, ct);
                if (result.Status == KyrolusRepositoryOperationStatus.NotFound)
                    return Results.NotFound();
                if (result.Status == KyrolusRepositoryOperationStatus.Failed && result.Exception is not null)
                    throw result.Exception;
                await uow.SaveChangesAsync(ct);
                return Results.Ok(result.Value);
            });

            // Restore (soft delete) if repo supports it
            group.MapPost("/{id}/restore", async (
                string id,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /restore/by-id with keys.");

                var repo = Repo(uow);
                if (repo is IKyrolusSingleKeySoftDeleteRepository<TEntity, TKey> singleSoft)
                {
                    var restoredSingle = await singleSoft.RestoreAsync(CastKey(ParseObject(id)), ct);
                    if (!restoredSingle) return Results.NotFound();
                    await uow.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> softRepo)
                {
                    var restored = await softRepo.RestoreAsync([ParseObject(id)], ct);
                    if (!restored) return Results.NotFound();
                    await uow.SaveChangesAsync(ct);
                    return Results.NoContent();
                }

                return Results.BadRequest("Restore not supported for this entity.");
            });

            group.MapPost("/{id}/try-restore", async (
                string id,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /try-restore/by-id with keys.");

                var repo = Repo(uow);
                if (repo is IKyrolusSingleKeySoftDeleteRepository<TEntity, TKey> singleSoft)
                {
                    var resultSingle = await singleSoft.TryRestoreAsync(CastKey(ParseObject(id)), ct);
                    return resultSingle.Status == KyrolusRepositoryOperationStatus.Success
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> softRepo)
                {
                    var result = await softRepo.TryRestoreAsync([ParseObject(id)], ct);
                    return result.Status == KyrolusRepositoryOperationStatus.Success
                        ? Results.NoContent()
                        : Results.NotFound();
                }

                return Results.BadRequest("Restore not supported for this entity.");
            });

            group.MapPost("/restore/by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                var repo = Repo(uow);
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> compositeSoft)
                {
                    var restoredComp = await compositeSoft.RestoreAsync(ResolveCompositeKeys(keys), ct);
                    if (!restoredComp) return Results.NotFound();
                    await uow.SaveChangesAsync(ct);
                    return Results.NoContent();
                }
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> softRepo)
                {
                    var restored = await softRepo.RestoreAsync(ResolveCompositeKeys(keys), ct);
                    if (!restored) return Results.NotFound();
                    await uow.SaveChangesAsync(ct);
                    return Results.NoContent();
                }

                return Results.BadRequest("Restore not supported for this entity.");
            });

            group.MapPost("/try-restore/by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                var repo = Repo(uow);
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> compositeSoft)
                {
                    var resultComp = await compositeSoft.TryRestoreAsync(ResolveCompositeKeys(keys), ct);
                    return resultComp.Status == KyrolusRepositoryOperationStatus.Success
                        ? Results.NoContent()
                        : Results.NotFound();
                }
                if (repo is IKyrolusCompositeKeySoftDeleteRepository<TEntity> softRepo)
                {
                    var result = await softRepo.TryRestoreAsync(ResolveCompositeKeys(keys), ct);
                    return result.Status == KyrolusRepositoryOperationStatus.Success
                        ? Results.NoContent()
                        : Results.NotFound();
                }

                return Results.BadRequest("Restore not supported for this entity.");
            });

            // Compiled queries (where available)
            group.MapGet("/compiled", (
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
                Results.BadRequest("Compiled GetAll requires a non-trivial filter. Use /all instead."));

            // Compiled get by id for single-key (route) or composite (query keys)
            group.MapGet("/{id}/compiled", async (
                string id,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                if (isComposite) return Results.BadRequest("Composite-key entities must use /compiled/by-id with keys.");

                var repo = Repo(uow) as IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey>;
                if (repo is null) return Results.BadRequest("Single-key endpoint requires single-key repo.");
                var entity = await repo.GetByIdCompiledAsync(CastKey(ParseObject(id)), ct);
                return entity is not null ? Results.Ok(entity) : Results.NotFound();
            });

            // Alternate compiled endpoint for composite keys without needing the route segment
            group.MapGet("/compiled/by-id", async (
                [FromQuery] string[] keys,
                IKyrolusUnitOfWork uow,
                CancellationToken ct) =>
            {
                var keyValues = ResolveCompositeKeys(keys);
                var repo = Repo(uow);
                if (keyValues.Length == 1 && !isComposite && repo is IKyrolusSingleKeyRepositoryAsync<ApplicationDbContext, TEntity, TKey> singleRepo)
                {
                    var entitySingle = await singleRepo.GetByIdCompiledAsync(CastKey(keyValues[0]), ct);
                    return entitySingle is not null ? Results.Ok(entitySingle) : Results.NotFound();
                }

                if (repo is IKyrolusCompositeKeyRepositoryAsync<ApplicationDbContext, TEntity, TRepoKey> compositeRepo)
                {
                    var entity = await compositeRepo.GetByIdAsync(keyValues, cancellationToken: ct);
                    return entity is not null ? Results.Ok(entity) : Results.NotFound();
                }

                return Results.BadRequest("Composite-key endpoint requires composite-key repo.");
            });

            // Existence check
            group.MapGet("/exists", async (
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromQuery] QueryRequest? request,
                CancellationToken ct) =>
            {
                var parts = helper.Build(request ?? new QueryRequest());
                var filter = parts.Filter ?? (_ => true);
                var exists = await Repo(uow).ExistAsync(filter, ct);
                return Results.Ok(exists);
            });

            // Stream (AsAsyncEnumerable)
            group.MapGet("/stream", async (
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromQuery] QueryRequest? request,
                CancellationToken ct) =>
            {
                var parts = helper.Build(request ?? new QueryRequest());
                var results = new List<TEntity>();
                await foreach (var item in Repo(uow).StreamAsync(
                    parts.Filter,
                    parts.OrderBy,
                    asNoTracking: parts.AsNoTracking ?? true,
                    useSplitQuery: parts.UseSplitQuery ?? false,
                    cancellationToken: ct))
                {
                    results.Add(item);
                }
                return Results.Ok(results);
            });

            // ExecuteUpdate using UpdateSettersBuilder (uses EF API with pragma to silence EF1001) / ExecuteDelete
            group.MapPost("/execute-update", async (
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromBody] ExecuteUpdateDto dto,
                CancellationToken ct) =>
            {
                var parts = helper.Build(dto.Request ?? new QueryRequest());
                if (dto.Updates is null || dto.Updates.Count == 0)
                    return Results.BadRequest("At least one property update is required.");

                var repo = Repo(uow);
#pragma warning disable EF1001 // UpdateSettersBuilder APIs are marked internal infrastructure by EF
                var affected = await repo.ExecuteUpdateAsync(
                    parts.Filter,
                    setters =>
                    {
                        foreach (var upd in dto.Updates)
                        {
                            if (string.IsNullOrWhiteSpace(upd.Property))
                                throw new ArgumentException("Property name is required.");

                            var prop = typeof(TEntity).GetProperty(upd.Property) ?? throw new ArgumentException($"Property '{upd.Property}' not found.");
                            var targetType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;
                            var valueObj = upd.Value is null ? null : Convert.ChangeType(upd.Value, targetType);

                            var param = Expression.Parameter(typeof(TEntity), "e");
                            var member = Expression.Property(param, prop);
                            var propLambda = Expression.Lambda(member, param);
                            var valueConst = Expression.Constant(valueObj, prop.PropertyType);
                            var valueLambda = Expression.Lambda(valueConst, param);

                            var setMethod = typeof(UpdateSettersBuilder<TEntity>).GetMethods()
                                .First(m => m.Name == "SetProperty" && m.GetParameters().Length == 2)
                                .MakeGenericMethod(prop.PropertyType);

                            setMethod.Invoke(setters, [propLambda, valueLambda]);
                        }
                    },
                    useSplitQuery: parts.UseSplitQuery,
                    cancellationToken: ct);
#pragma warning restore EF1001
                await uow.SaveChangesAsync(ct);
                return Results.Ok(affected);
            });

            group.MapPost("/execute-delete", async (
                IKyrolusUnitOfWork uow,
                IQueryHelper<TEntity> helper,
                [FromBody] QueryRequest request,
                CancellationToken ct) =>
            {
                var parts = helper.Build(request);
                var affected = await Repo(uow).ExecuteDeleteAsync(parts.Filter, parts.UseSplitQuery, ct);
                await uow.SaveChangesAsync(ct);
                return Results.Ok(affected);
            });

            return group;

            static TRepo Repo(IKyrolusUnitOfWork uow)
                => uow.GetRepository<TRepo>();

            static object?[] ResolveCompositeKeys(string[] keys)
            {
                if (keys is { Length: > 0 })
                {
                    return [.. keys.Select(ParseObject)];
                }
                throw new ArgumentException("Key(s) are required.");
            }

            static object? ParseObject(string raw)
            {
                if (Guid.TryParse(raw, out var g)) return g;
                if (int.TryParse(raw, out var i)) return i;
                if (long.TryParse(raw, out var l)) return l;
                if (bool.TryParse(raw, out var b)) return b;
                return raw;
            }

            static TKey CastKey(object? raw)
            {
                ArgumentNullException.ThrowIfNull(raw);

                if (raw is TKey t) return t;
                if (raw is Guid g && typeof(TKey) == typeof(Guid)) return (TKey)(object)g;

                var converted = Convert.ChangeType(raw, typeof(TKey)) ?? throw new InvalidCastException($"Cannot convert value to {typeof(TKey).FullName}.");

                return (TKey)converted;
            }
        }
    }
#pragma warning restore S1144
#pragma warning restore S1192
#pragma warning restore S3776
}

public sealed record ExecuteUpdateDto(QueryRequest? Request, List<PropertyUpdate> Updates);
public sealed record PropertyUpdate(string Property, object? Value);






