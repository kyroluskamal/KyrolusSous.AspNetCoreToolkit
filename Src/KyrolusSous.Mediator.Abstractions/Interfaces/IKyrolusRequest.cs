namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Non-generic marker implemented by every request. Lets code ask "is this a request at all?"
/// without knowing the response type, which C# cannot express as <c>is IKyrolusRequest&lt;&gt;</c>.
/// </summary>
public interface IKyrolusRequestBase
{
}

/// <summary>
/// A message handled by exactly one handler, producing a <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface IKyrolusRequest<out TResponse> : IKyrolusRequestBase
{
}

/// <summary>
/// A request that produces no value. Equivalent to <see cref="IKyrolusRequest{TResponse}"/> of
/// <see cref="Unit"/>.
/// </summary>
public interface IKyrolusRequest : IKyrolusRequest<Unit>
{
}
