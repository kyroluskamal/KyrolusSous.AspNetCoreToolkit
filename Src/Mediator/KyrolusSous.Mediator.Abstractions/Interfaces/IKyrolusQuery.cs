namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Non-generic marker implemented by every query. Lets code ask "is this a query at all?"
/// without knowing the response type, which C# cannot express as <c>is IKyrolusQuery&lt;&gt;</c>.
/// </summary>
/// <remarks>
/// This is the check the caching behavior makes before it does anything: a cache may serve a
/// query twice, but must never stand in for a command.
/// </remarks>
public interface IKyrolusQueryBase
{
}

/// <summary>
/// A message that reads data and changes nothing - "get this user", "list open orders".
/// Handled by exactly one handler.
/// </summary>
/// <remarks>
/// Name queries as a request for information: <c>GetUser</c>, <c>ListOpenOrders</c>.
/// <para>
/// There is no value-less variant, unlike <see cref="IKyrolusCommand"/>: a query that returns
/// nothing has done nothing. An operation that returns nothing is changing something, which makes
/// it a command.
/// </para>
/// <para>
/// "Changes nothing" is a promise the type system cannot enforce - nothing stops a handler from
/// writing to the database. Breaking it will misbehave in practice, because caching and retry
/// behaviors both assume a query can safely run twice.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">
/// The type produced by the handler. Covariant (<c>out</c>): a <c>IKyrolusQuery&lt;Dog&gt;</c> is
/// usable where a <c>IKyrolusQuery&lt;Animal&gt;</c> is expected.
/// </typeparam>
/// <example>
/// <code>
/// public record GetUser(Guid Id) : IKyrolusQuery&lt;User?&gt;;
///
/// User? user = await mediator.SendAsync(new GetUser(id));
/// </code>
/// </example>
public interface IKyrolusQuery<out TResponse> : IKyrolusRequest<TResponse>, IKyrolusQueryBase
{
}
