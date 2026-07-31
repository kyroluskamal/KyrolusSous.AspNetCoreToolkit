namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// A request whose handler produces items one at a time instead of one finished result - reading
/// a million rows, tailing a log, relaying tokens from an AI model.
/// </summary>
/// <remarks>
/// The difference from <see cref="IKyrolusQuery{TResponse}"/> is when the caller gets data. A
/// query hands back everything at once, so all of it sits in memory first and nothing is usable
/// until the last row arrives. A stream hands over each item as it is produced, so memory stays
/// flat and the caller can start work immediately.
/// <para>
/// Note <typeparamref name="TResponse"/> is the type of <em>one item</em>, not of the collection:
/// a stream of users is <c>IKyrolusStreamRequest&lt;User&gt;</c>, not
/// <c>IKyrolusStreamRequest&lt;List&lt;User&gt;&gt;</c>.
/// </para>
/// <para>
/// Streams deliberately sit outside <see cref="IKyrolusRequest{TResponse}"/>. They run through
/// <see cref="IKyrolusStreamPipelineBehavior{TRequest, TResponse}"/>, not the ordinary pipeline,
/// because a behavior that wraps a single result cannot meaningfully wrap an open sequence.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">
/// The type of a single streamed item. Covariant (<c>out</c>): produced, never consumed.
/// </typeparam>
/// <example>
/// <code>
/// public record ExportUsers(DateOnly Since) : IKyrolusStreamRequest&lt;User&gt;;
///
/// await foreach (var user in mediator.StreamAsync(new ExportUsers(since), cancellationToken))
/// {
///     await writer.WriteLineAsync(user.Email);   // handled as each row arrives
/// }
/// </code>
/// </example>
public interface IKyrolusStreamRequest<out TResponse>
{
}
