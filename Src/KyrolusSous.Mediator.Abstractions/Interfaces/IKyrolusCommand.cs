namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Non-generic marker implemented by every command. Lets code ask "is this a command at all?"
/// without knowing the response type, which C# cannot express as <c>is IKyrolusCommand&lt;&gt;</c>.
/// </summary>
/// <remarks>
/// Without it, answering that question would need reflection on every message passing through the
/// pipeline. Cache invalidation and handler resolution both rely on this check.
/// </remarks>
public interface IKyrolusCommandBase
{
}

/// <summary>
/// A message that changes something and returns no value - "delete this user", "mark as read".
/// Handled by exactly one handler.
/// </summary>
/// <remarks>
/// Name commands in the imperative, as an instruction: <c>DeleteUser</c>, not <c>UserDeleted</c>
/// (that would be a notification - something that already happened).
/// <para>
/// Equivalent to <see cref="IKyrolusRequest{TResponse}"/> of <see cref="Unit"/>, but written this
/// way so <see cref="Unit"/> never appears in your code.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public record DeleteUser(Guid Id) : IKyrolusCommand;
///
/// await mediator.SendAsync(new DeleteUser(id));   // nothing comes back
/// </code>
/// </example>
public interface IKyrolusCommand : IKyrolusRequest<Unit>, IKyrolusCommandBase
{
}

/// <summary>
/// A message that changes something and returns a value - usually the id of what was created, or
/// the created entity itself. Handled by exactly one handler.
/// </summary>
/// <remarks>
/// Returning a value does not make this a query. What decides the split is whether the message
/// <em>changes</em> anything: creating a user changes state, so it is a command even though it
/// hands back an id.
/// <para>
/// The distinction is not decoration. Caching behaviors deliberately skip commands, because
/// caching an operation that changes state would serve a stale result and skip the change.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">
/// The type produced by the handler. Covariant (<c>out</c>): a
/// <c>IKyrolusCommand&lt;Dog&gt;</c> is usable where a <c>IKyrolusCommand&lt;Animal&gt;</c> is expected.
/// </typeparam>
/// <example>
/// <code>
/// public record CreateUser(string Email) : IKyrolusCommand&lt;Guid&gt;;
///
/// Guid newId = await mediator.SendAsync(new CreateUser("a@b.com"));
/// </code>
/// </example>
public interface IKyrolusCommand<out TResponse> : IKyrolusRequest<TResponse>, IKyrolusCommandBase
{
}
