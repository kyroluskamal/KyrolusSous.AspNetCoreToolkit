namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// Handles a command that changes state and returns a value - typically the id of what was
/// created. Exactly one handler may exist per command type.
/// </summary>
/// <remarks>
/// Adds no members of its own: it exists so the command/query split lives in the type system
/// rather than in a naming convention. The compiler will not let a command handler be declared
/// against a query, and the dispatcher uses the distinction to resolve the right handler.
/// <para>
/// Use this when the caller needs something back. When it does not, use
/// <see cref="IKyrolusCommandHandler{TCommand}"/> so no response type is involved at all.
/// </para>
/// </remarks>
/// <typeparam name="TCommand">The command type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <typeparam name="TResponse">The type the handler produces.</typeparam>
/// <example>
/// <code>
/// public record CreateUser(string Email) : IKyrolusCommand&lt;Guid&gt;;
///
/// public class CreateUserHandler(AppDbContext db) : IKyrolusCommandHandler&lt;CreateUser, Guid&gt;
/// {
///     public async Task&lt;Guid&gt; Handle(CreateUser command, CancellationToken cancellationToken)
///     {
///         var user = new User { Email = command.Email };
///         db.Users.Add(user);
///         await db.SaveChangesAsync(cancellationToken);
///         return user.Id;
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusCommandHandler<in TCommand, TResponse> : IKyrolusRequestHandler<TCommand, TResponse>
    where TCommand : IKyrolusCommand<TResponse>
{
}

/// <summary>
/// Handles a command that changes state and returns nothing - a delete or a status update, say.
/// </summary>
/// <remarks>
/// The handler returns a plain <see cref="Task"/>, so <see cref="Unit"/> stays an implementation
/// detail of the mediator and never appears in your code.
/// </remarks>
/// <typeparam name="TCommand">The command type. Contravariant (<c>in</c>): consumed, never returned.</typeparam>
/// <example>
/// <code>
/// public record DeleteUser(Guid Id) : IKyrolusCommand;
///
/// public class DeleteUserHandler(AppDbContext db) : IKyrolusCommandHandler&lt;DeleteUser&gt;
/// {
///     public async Task Handle(DeleteUser command, CancellationToken cancellationToken)
///         =&gt; await db.Users.Where(u =&gt; u.Id == command.Id).ExecuteDeleteAsync(cancellationToken);
/// }
/// </code>
/// </example>
public interface IKyrolusCommandHandler<in TCommand> : IKyrolusRequestHandler<TCommand>
    where TCommand : IKyrolusCommand
{
}
