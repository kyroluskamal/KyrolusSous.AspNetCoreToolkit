namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a command as idempotent, ensuring that multiple requests with the same idempotency key are executed only once.
/// </summary>
public interface IKyrolusIdempotentCommand : IKyrolusCommand
{
    /// <summary>
    /// Unique identifier for the command execution (e.g., client-supplied request token or UUID).
    /// </summary>
    string IdempotencyKey { get; }

    /// <summary>
    /// Optional cache duration for the idempotent result. If null, a default TTL (e.g. 24 hours) is used.
    /// </summary>
    TimeSpan? IdempotencyTtl => null;
}

/// <summary>
/// Marks a command returning <typeparamref name="TResponse"/> as idempotent.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IKyrolusIdempotentCommand<TResponse> : IKyrolusCommand<TResponse>
{
    /// <summary>
    /// Unique identifier for the command execution.
    /// </summary>
    string IdempotencyKey { get; }

    /// <summary>
    /// Optional cache duration for the idempotent result.
    /// </summary>
    TimeSpan? IdempotencyTtl => null;
}
