using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Marten.Command.Add;
using KyrolusSous.CQRS.Marten.Command.Remove;
using KyrolusSous.CQRS.Marten.Command.SoftDelete;
using KyrolusSous.CQRS.Marten.Command.Update;
using KyrolusSous.CQRS.Marten.Query;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;
using KyrolusSous.ExceptionHandling.Abstractions;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Infrastructure;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Models;
using KyrolusSous.Marten.Runtime.FullPipeline.TestApp.Services;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Records;
using Marten;
using System.Linq.Expressions;

namespace KyrolusSous.Marten.Runtime.FullPipeline.TestApp.CQRS.MenuItems;

public sealed class AddMenuItemHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver)
    : IKyrolusCommandHandler<AddCommand<MenuItem>, MenuItem>
{
    public async Task<MenuItem> Handle(AddCommand<MenuItem> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var entity = command.Entity ?? throw new ArgumentNullException(nameof(command.Entity));
        if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
        if (string.IsNullOrWhiteSpace(entity.TenantId))
            entity.TenantId = tenantResolver.ResolveTenantId() ?? string.Empty;

        var result = await repo.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}

public sealed class AddMenuItemRangeHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver)
    : IKyrolusCommandHandler<AddRangeCommand<MenuItem>, IEnumerable<MenuItem>>
{
    public async Task<IEnumerable<MenuItem>> Handle(AddRangeCommand<MenuItem> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var entities = command.Entities?.ToList() ?? throw new ArgumentNullException(nameof(command.Entities));
        var tenant = tenantResolver.ResolveTenantId() ?? string.Empty;

        foreach (var entity in entities)
        {
            if (entity.Id == Guid.Empty) entity.Id = Guid.NewGuid();
            if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = tenant;
        }

        var result = await repo.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}

public sealed class UpdateMenuItemHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver)
    : IKyrolusCommandHandler<UpdateCommand<MenuItem>, MenuItem>
{
    public async Task<MenuItem> Handle(UpdateCommand<MenuItem> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var entity = command.Entity ?? throw new ArgumentNullException(nameof(command.Entity));
        if (string.IsNullOrWhiteSpace(entity.TenantId))
            entity.TenantId = tenantResolver.ResolveTenantId() ?? string.Empty;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var result = await repo.UpdateAsync(entity, command.ExpectedVersion, tenantId: null, cancellationToken)
            .ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result ?? entity;
    }
}

public sealed class UpdateMenuItemRangeHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver)
    : IKyrolusCommandHandler<UpdateRangeCommand<MenuItem>, IEnumerable<MenuItem>>
{
    public async Task<IEnumerable<MenuItem>> Handle(UpdateRangeCommand<MenuItem> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var entities = command.Entities?.ToList() ?? throw new ArgumentNullException(nameof(command.Entities));
        var tenant = tenantResolver.ResolveTenantId() ?? string.Empty;

        foreach (var entity in entities)
        {
            if (string.IsNullOrWhiteSpace(entity.TenantId)) entity.TenantId = tenant;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        var result = await repo.UpdateRangeAsync(entities, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}

public sealed class RemoveMenuItemByIdHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
    : IKyrolusCommandHandler<RemoveByIdCommand<MenuItem, Guid>>
{
    public async Task Handle(RemoveByIdCommand<MenuItem, Guid> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        await repo.RemoveAsync(command.Id, command.ExpectedVersion, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class RemoveMenuItemRangeHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
    : IKyrolusCommandHandler<RemoveRangeCommand<MenuItem>>
{
    public async Task Handle(RemoveRangeCommand<MenuItem> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        await repo.RemoveRangeAsync(command.Entities, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}

public sealed class PatchMenuItemHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
    : IKyrolusCommandHandler<MenuItemPatchCommand, MenuItem>
{
    public async Task<MenuItem> Handle(MenuItemPatchCommand command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var result = await repo.PatchAsync(command.Id, command.Updates, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result?.Entity ?? throw new KyrolusNotFoundException(nameof(MenuItem), command.Id.ToString());
    }
}

public sealed class SoftDeleteMenuItemByIdHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
    : IKyrolusCommandHandler<SoftDeleteByIdCommand<MenuItem, Guid>, bool>
{
    public async Task<bool> Handle(SoftDeleteByIdCommand<MenuItem, Guid> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var id = ResolveKey(command.KeyValues);
        var result = await repo.RemoveAsync(id, expectedVersion: null, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static Guid ResolveKey(object?[]? keyValues)
        => keyValues is { Length: > 0 } && keyValues[0] is Guid id
            ? id
            : throw new ArgumentException("KeyValues must contain the Guid id.", nameof(keyValues));
}

public sealed class RestoreMenuItemByIdHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork)
    : IKyrolusCommandHandler<RestoreByIdCommand<MenuItem, Guid>, bool>
{
    public async Task<bool> Handle(RestoreByIdCommand<MenuItem, Guid> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var id = ResolveKey(command.KeyValues);
        var result = await repo.RestoreAsync(id, tenantId: null, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static Guid ResolveKey(object?[]? keyValues)
        => keyValues is { Length: > 0 } && keyValues[0] is Guid id
            ? id
            : throw new ArgumentException("KeyValues must contain the Guid id.", nameof(keyValues));
}

public sealed class GetMenuItemsHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver,
    ICacheProvider cacheProvider)
    : IKyrolusQueryHandler<GetAllQuery<MenuItem>, IEnumerable<MenuItem>>
{
    public async Task<IEnumerable<MenuItem>> Handle(GetAllQuery<MenuItem> query, CancellationToken cancellationToken)
    {
        var tenant = query.TenantId ?? tenantResolver.ResolveTenantId() ?? string.Empty;
        var opts = BuildOptions(query, tenant);
        var useCache = ShouldUseCache(query);

        if (useCache)
        {
            var cacheKey = CacheKeys.MenuItemsAll(tenant);
            return await cacheProvider.GetOrCreateAsync(
                cacheKey,
                _ => FetchAsync(query, opts, cancellationToken),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await FetchAsync(query, opts, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IEnumerable<MenuItem>> FetchAsync(
        GetAllQuery<MenuItem> query,
        MartenQueryOptions<MenuItem> opts,
        CancellationToken cancellationToken)
    {
        if (query.DeletedOnly)
        {
            var soft = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            return await soft.GetDeletedOnlyAsync(opts, cancellationToken).ConfigureAwait(false);
        }

        if (query.IncludeDeleted)
        {
            var soft = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            return await soft.GetAllIncludingDeletedAsync(opts, cancellationToken).ConfigureAwait(false);
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        return await repo.GetAllAsync(opts, cancellationToken).ConfigureAwait(false);
    }

    private static MartenQueryOptions<MenuItem> BuildOptions(GetAllQuery<MenuItem> query, string tenant)
        => new(
            Filter: query.Filter,
            OrderBy: query.OrderBy,
            IncludeProperties: query.IncludeProperties,
            IncludeExpressions: query.IncludeExpressions,
            TenantId: string.IsNullOrWhiteSpace(tenant) ? query.TenantId : tenant,
            IncludeSoftDeleted: query.IncludeDeleted || query.DeletedOnly);

    private static bool ShouldUseCache(GetAllQuery<MenuItem> query)
    {
        var filterOk = query.Filter is null
            || (query.TenantId is not null && IsTenantOnlyFilter(query.Filter, query.TenantId));

        return filterOk
            && query.OrderBy is null
            && (query.IncludeProperties is null || query.IncludeProperties.Count == 0)
            && (query.IncludeExpressions is null || query.IncludeExpressions.Length == 0)
            && !query.IncludeDeleted
            && !query.DeletedOnly;
    }

    private static bool IsTenantOnlyFilter(Expression<Func<MenuItem, bool>> filter, string tenantId)
    {
        if (filter.Body is not BinaryExpression { NodeType: ExpressionType.Equal } binary)
            return false;

        if (binary.Left is not MemberExpression left || !string.Equals(left.Member.Name, "TenantId", StringComparison.OrdinalIgnoreCase))
            return false;

        if (binary.Right is ConstantExpression constant && constant.Value is string value)
            return string.Equals(value, tenantId, StringComparison.Ordinal);

        return false;
    }
}

public sealed class GetMenuItemByIdHandler(
    IKyrolusMartenUnitOfWork<IDocumentSession> unitOfWork,
    ITenantResolver tenantResolver,
    ICacheProvider cacheProvider)
    : IKyrolusQueryHandler<GetByIdQuery<MenuItem, Guid>, MenuItem?>
{
    public async Task<MenuItem?> Handle(GetByIdQuery<MenuItem, Guid> query, CancellationToken cancellationToken)
    {
        var tenant = query.TenantId ?? tenantResolver.ResolveTenantId() ?? string.Empty;
        var opts = new MartenQueryOptions<MenuItem>(
            IncludeProperties: query.IncludeProperties,
            IncludeExpressions: query.IncludeExpressions,
            TenantId: tenant,
            IncludeSoftDeleted: query.IncludeDeleted);

        if (!query.IncludeDeleted && query.IncludeExpressions is null && (query.IncludeProperties?.Count ?? 0) == 0)
        {
            var cacheKey = CacheKeys.MenuItemById(tenant, query.Id);
            return await cacheProvider.GetOrCreateAsync(
                cacheKey,
                _ => FetchAsync(query, opts, cancellationToken),
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        return await FetchAsync(query, opts, cancellationToken).ConfigureAwait(false);
    }

    private async Task<MenuItem?> FetchAsync(
        GetByIdQuery<MenuItem, Guid> query,
        MartenQueryOptions<MenuItem> opts,
        CancellationToken cancellationToken)
    {
        if (query.IncludeDeleted)
        {
            var soft = unitOfWork.GetRepository<IKyrolusMartenSoftDeleteRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
            var result = await soft.GetByIdIncludingDeletedAsync(query.Id, opts, cancellationToken).ConfigureAwait(false);
            return result?.Entity;
        }

        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<IDocumentSession, MenuItem, Guid>>();
        var entityResult = await repo.GetByIdAsync(query.Id, opts, cancellationToken).ConfigureAwait(false);
        return entityResult?.Entity;
    }
}
