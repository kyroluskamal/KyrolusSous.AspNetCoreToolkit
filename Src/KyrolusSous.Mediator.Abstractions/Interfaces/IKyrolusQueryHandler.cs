namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Handles a query - a read that returns data and leaves state untouched. Exactly one handler may
/// exist per query type.
/// </summary>
/// <remarks>
/// Adds no members of its own: it exists so the command/query split lives in the type system
/// rather than in a naming convention. Marking a read as a query is what lets caching behaviors
/// recognise it, since caching a command would be wrong.
/// <para>
/// There is no response-less variant, unlike <see cref="IKyrolusCommandHandler{TCommand}"/>: a
/// query that returns nothing has done nothing. If an operation returns nothing it is changing
/// something, which makes it a command.
/// </para>
/// <para>
/// "Leaves state untouched" is a promise this interface cannot enforce - nothing stops a handler
/// from writing to the database. Breaking it means caching and retry behaviors will misbehave,
/// because both assume a query can safely run twice.
/// </para>
/// </remarks>
/// <typeparam name="TQuery">The query type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TResponse">The type the handler produces.</typeparam>
/// <example>
/// <code>
/// public record GetUser(Guid Id) : IKyrolusQuery&lt;User?&gt;;
///
/// public class GetUserHandler(AppDbContext db) : IKyrolusQueryHandler&lt;GetUser, User?&gt;
/// {
///     public async Task&lt;User?&gt; Handle(GetUser query, CancellationToken cancellationToken)
///         =&gt; await db.Users
///             .AsNoTracking()
///             .FirstOrDefaultAsync(u =&gt; u.Id == query.Id, cancellationToken);
/// }
/// </code>
/// </example>
public interface IKyrolusQueryHandler<in TQuery, TResponse> : IKyrolusRequestHandler<TQuery, TResponse>
    where TQuery : IKyrolusQuery<TResponse>
{
}
