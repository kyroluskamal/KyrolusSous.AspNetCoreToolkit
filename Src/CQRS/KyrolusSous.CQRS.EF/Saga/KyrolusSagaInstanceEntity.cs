using KyrolusSous.CQRS.Saga;

namespace KyrolusSous.CQRS.EF.Saga;

/// <summary>
/// Persisted shape of a <see cref="KyrolusSagaInstance"/>, mapped by
/// <see cref="KyrolusSagaModelBuilderExtensions.ApplyKyrolusSagaStore"/> and read/written by
/// <see cref="EfSagaStore{TDbContext}"/>.
/// </summary>
/// <remarks>
/// A separate type rather than mapping <see cref="KyrolusSagaInstance"/> directly: that type's
/// <c>Id</c>/<c>SagaName</c>/<c>StartedAtUtc</c> are <see langword="init"/>-only, which EF Core can map
/// only via constructor binding - a materialization strategy this package should not force onto every
/// consumer just for this one entity, and one that would silently break the moment
/// <see cref="KyrolusSagaInstance"/>'s own shape changes for reasons that have nothing to do with
/// persistence. Keeping the two shapes distinct also means a change to the domain type's shape cannot
/// silently change this store's column layout (or vice versa) without an explicit edit to
/// <see cref="EfSagaStore{TDbContext}"/>'s own mapping methods.
/// </remarks>
public sealed class KyrolusSagaInstanceEntity
{
    /// <summary>Mirrors <see cref="KyrolusSagaInstance.Id"/> - the table's primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.SagaName"/>.</summary>
    public required string SagaName { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.ContextJson"/>.</summary>
    public required string ContextJson { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.CurrentStepIndex"/>.</summary>
    public int CurrentStepIndex { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.Status"/>.</summary>
    public KyrolusSagaStatus Status { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.StartedAtUtc"/>.</summary>
    public DateTimeOffset StartedAtUtc { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.CompletedAtUtc"/>.</summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.Error"/>.</summary>
    public string? Error { get; set; }

    /// <summary>
    /// Mirrors <see cref="KyrolusSagaInstance.CorrelationId"/> - indexed by
    /// <see cref="KyrolusSagaModelBuilderExtensions.ApplyKyrolusSagaStore"/>, since it is what
    /// <see cref="EfSagaStore{TDbContext}.GetByCorrelationIdAsync"/> queries by.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.StepCountAtStart"/>.</summary>
    public int StepCountAtStart { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.StepSignatureAtStart"/>.</summary>
    public string? StepSignatureAtStart { get; set; }

    /// <summary>
    /// Mirrors <see cref="KyrolusSagaInstance.Version"/> - the column
    /// <see cref="EfSagaStore{TDbContext}.SaveAsync"/> gates its conditional update on.
    /// </summary>
    public int Version { get; set; }
}
