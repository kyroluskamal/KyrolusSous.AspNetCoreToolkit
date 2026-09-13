using KyrolusSous.CQRS.Saga;
using Marten.Schema;

namespace KyrolusSous.CQRS.Marten.Saga;

/// <summary>
/// Marten document backing <see cref="MartenSagaStore"/>: the persisted shape of a
/// <see cref="KyrolusSagaInstance"/>.
/// </summary>
/// <remarks>
/// Implements <see cref="JasperFx.IRevisioned"/> - not <see cref="JasperFx.Metadata.IVersioned"/> (a
/// <see cref="Guid"/> ETag) or <see cref="JasperFx.ILongVersioned"/> (a <see cref="long"/> revision,
/// meant for versions sourced from a multi-stream projection's event sequence number) - specifically
/// because <see cref="JasperFx.IRevisioned.Version"/> is an <see cref="int"/>, exactly matching
/// <see cref="KyrolusSagaInstance.Version"/>'s own type: no translation between "the number
/// <see cref="IKyrolusSagaStore.SaveAsync"/>'s contract hands callers" and "the number Marten tracks
/// for its own optimistic-concurrency check" is needed. Implementing this marker interface is enough by
/// itself for Marten to enforce optimistic concurrency on every plain <c>session.Store(document)</c> +
/// <c>SaveChangesAsync()</c> - see <see cref="MartenSagaStore.SaveAsync"/> for how that plays out.
/// </remarks>
public sealed class KyrolusMartenSagaInstanceDocument : JasperFx.IRevisioned
{
    /// <summary>Mirrors <see cref="KyrolusSagaInstance.Id"/> - Marten's default document identity property.</summary>
    public Guid Id { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.SagaName"/>.</summary>
    public string SagaName { get; set; } = string.Empty;

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.ContextJson"/>.</summary>
    public string ContextJson { get; set; } = string.Empty;

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
    /// Mirrors <see cref="KyrolusSagaInstance.CorrelationId"/>. Marked
    /// <see cref="DuplicateFieldAttribute"/> - duplicated out of the JSONB <c>data</c> column into its
    /// own indexed column - since it is the only field <see cref="MartenSagaStore"/> queries by (see
    /// <see cref="MartenSagaStore.GetByCorrelationIdAsync"/>) rather than loading by
    /// <see cref="Id"/> or scanning by <see cref="Status"/>.
    /// </summary>
    [DuplicateField]
    public string? CorrelationId { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.StepCountAtStart"/>.</summary>
    public int StepCountAtStart { get; set; }

    /// <summary>Mirrors <see cref="KyrolusSagaInstance.StepSignatureAtStart"/>.</summary>
    public string? StepSignatureAtStart { get; set; }

    /// <summary>
    /// Mirrors <see cref="KyrolusSagaInstance.Version"/> - and, via <see cref="JasperFx.IRevisioned"/>,
    /// is also the property Marten itself reads and writes to enforce optimistic concurrency. See this
    /// type's remarks.
    /// </summary>
    public int Version { get; set; }
}
