namespace KyrolusSous.CQRS.Mapping.Contracts;

/// <summary>
/// Non-generic allow-list surface shared by <see cref="IKyrolusMappedCommand{TEntity}"/> and
/// <see cref="IKyrolusMappedCommand{TEntity, TResponse}"/>, so <c>ApplyTo</c> can check for it without
/// needing to know the command's <c>TEntity</c> type argument at the call site.
/// </summary>
/// <remarks>
/// Mirrors <c>IKyrolusPropertyUpdateRequest</c> in <c>KyrolusSous.CQRS.Abstractions</c>: opt-in,
/// case-insensitive, <see langword="null"/> by default so a command that never sets
/// <see cref="AllowedProperties"/> keeps today's fully unrestricted <c>ApplyTo</c>/<c>ToEntity</c>
/// behavior exactly. A command that maps onto a persisted entity SHOULD set this - without it, any
/// public property the mapper can read from the command and write onto the entity's matching property
/// name gets copied over with zero configuration (see <see cref="KyrolusCqrsMappingExtensions.ApplyTo{TTarget}(IKyrolusMapTo{TTarget}, TTarget, IKyrolusObjectMapper, KyrolusMappingContext?)"/>).
/// </remarks>
public interface IKyrolusAllowListedMappedCommand
{
    /// <summary>
    /// The only property names this command may write onto its mapped target, or <see langword="null"/>
    /// to leave the command unrestricted (the default). Matched case-insensitively.
    /// </summary>
    IReadOnlySet<string>? AllowedProperties => null;
}

/// <summary>
/// Defines a CQRS command that maps directly to a destination domain entity <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The target domain entity type.</typeparam>
public interface IKyrolusMappedCommand<TEntity> : IKyrolusCommand, IKyrolusMapTo<TEntity>, IKyrolusAllowListedMappedCommand
{
}

/// <summary>
/// Defines a CQRS command that maps to a destination domain entity <typeparamref name="TEntity"/> and yields a response <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TEntity">The target domain entity type.</typeparam>
/// <typeparam name="TResponse">The response return type.</typeparam>
public interface IKyrolusMappedCommand<TEntity, out TResponse> : IKyrolusCommand<TResponse>, IKyrolusMapTo<TEntity>, IKyrolusAllowListedMappedCommand
{
}
