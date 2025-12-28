using System.Text.Json;

namespace KyrolusSous.Repositories.Marten.Runtime.Saga;

public sealed class KyrolusMartenSagaCoordinator : IKyrolusMartenSagaCoordinator
{
    private readonly IDocumentSession session;

    public KyrolusMartenSagaCoordinator(IDocumentSession session)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
    }

    public async Task<Guid> StartAsync(object sagaState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sagaState);
        var envelope = new KyrolusMartenSagaEnvelope
        {
            Id = Guid.NewGuid(),
            Type = sagaState.GetType().AssemblyQualifiedName,
            Payload = JsonSerializer.Serialize(sagaState),
            Completed = false,
            UpdatedAt = DateTime.UtcNow
        };
        session.Store(envelope);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return envelope.Id;
    }

    public async Task<bool> ContinueAsync(Guid sagaId, object message, CancellationToken cancellationToken = default)
    {
        var envelope = await session.LoadAsync<KyrolusMartenSagaEnvelope>(sagaId, cancellationToken).ConfigureAwait(false);
        if (envelope is null || envelope.Completed) return false;
        envelope.Type = message.GetType().AssemblyQualifiedName;
        envelope.Payload = JsonSerializer.Serialize(message);
        envelope.UpdatedAt = DateTime.UtcNow;
        session.Store(envelope);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<object?> GetStateAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var envelope = await session.LoadAsync<KyrolusMartenSagaEnvelope>(sagaId, cancellationToken).ConfigureAwait(false);
        if (envelope?.Payload is null || string.IsNullOrEmpty(envelope.Type)) return null;
        var type = Type.GetType(envelope.Type);
        return type is null ? envelope.Payload : JsonSerializer.Deserialize(envelope.Payload, type);
    }

    public async Task<bool> CompleteAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        var envelope = await session.LoadAsync<KyrolusMartenSagaEnvelope>(sagaId, cancellationToken).ConfigureAwait(false);
        if (envelope is null) return false;
        envelope.Completed = true;
        envelope.UpdatedAt = DateTime.UtcNow;
        session.Store(envelope);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
