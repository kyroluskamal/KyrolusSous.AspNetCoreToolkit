namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Handles a streaming request, yielding items as they become available. Exactly one handler may
/// exist per stream request type.
/// </summary>
/// <remarks>
/// Written with <c>async</c> plus <c>yield return</c>: each <c>yield return</c> hands one item to
/// the caller and pauses there until the caller asks for the next one. Nothing is buffered, so
/// memory stays flat no matter how many items there are.
/// <para>
/// <b>Cancellation is yours to check.</b> The token is passed in, but nothing stops the loop on
/// your behalf - call <c>cancellationToken.ThrowIfCancellationRequested()</c> each iteration, or
/// the stream keeps producing after the caller has walked away.
/// </para>
/// <para>
/// The <c>[EnumeratorCancellation]</c> attribute on the token parameter is required. It is what
/// lets a token passed to <c>WithCancellation(...)</c> at the call site reach this method; without
/// it the parameter silently stays <c>default</c> and cancellation never arrives.
/// </para>
/// </remarks>
/// <typeparam name="TRequest">The stream request type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TResponse">The type of a single streamed item. Covariant (<c>out</c>): produced, never consumed.</typeparam>
/// <example>
/// <code>
/// public class ExportUsersHandler(AppDbContext db) : IKyrolusStreamRequestHandler&lt;ExportUsers, User&gt;
/// {
///     public async IAsyncEnumerable&lt;User&gt; Handle(
///         ExportUsers request,
///         [EnumeratorCancellation] CancellationToken cancellationToken)
///     {
///         var query = db.Users
///             .Where(u =&gt; u.CreatedOn &gt;= request.Since)
///             .AsAsyncEnumerable();
///
///         await foreach (var user in query.WithCancellation(cancellationToken))
///         {
///             cancellationToken.ThrowIfCancellationRequested();
///             yield return user;
///         }
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusStreamRequestHandler<in TRequest, out TResponse>
    where TRequest : IKyrolusStreamRequest<TResponse>
{
    /// <summary>Produces the stream. Nothing runs until the caller starts enumerating.</summary>
    /// <param name="request">The stream request.</param>
    /// <param name="cancellationToken">
    /// Mark this parameter <c>[EnumeratorCancellation]</c> in your implementation, and check it
    /// inside the loop - cancellation is cooperative.
    /// </param>
    /// <returns>The items, produced one at a time.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
