namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// An announcement that something already happened. Any number of handlers may listen, including
/// none, and none of them returns anything.
/// </summary>
/// <remarks>
/// The opposite of a command in both direction and grammar. A command is an instruction aimed at
/// one handler - <c>DeleteUser</c>. A notification is news, aimed at nobody in particular, and its
/// name is in the past tense - <c>UserDeleted</c>.
/// <para>
/// What it buys you is that the publisher stops knowing who listens. A handler that creates a
/// user publishes <c>UserCreated</c> and is done; welcome emails, audit entries and dashboard
/// counters each live in their own class and are discovered through dependency injection. Adding
/// a fifth listener is a new file, not an edit to the create-user handler.
/// </para>
/// <para>
/// This is publish/subscribe <em>inside one process</em>, in memory, right now - not a message
/// broker. Handlers run before <c>PublishAsync</c> returns, and nothing survives a crash. For
/// events that must cross a service boundary or outlive a restart, publish to a broker (a single
/// notification handler forwarding to RabbitMQ is the usual bridge).
/// </para>
/// <para>
/// Publishing with no handlers registered is not an error - it does nothing. That is the intended
/// behaviour: news with no listeners is simply unheard.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public record UserCreated(Guid Id, string Email) : INotification;
///
/// // In the command handler, after the user is safely saved:
/// await mediator.PublishAsync(new UserCreated(user.Id, user.Email), cancellationToken);
/// </code>
/// </example>
public interface INotification
{
}
