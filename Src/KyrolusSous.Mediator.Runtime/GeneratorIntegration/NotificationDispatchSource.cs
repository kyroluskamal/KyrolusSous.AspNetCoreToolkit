namespace KyrolusSous.Mediator.Runtime.GeneratorIntegration;

/// <summary>
/// Turns a notification into the list of handler calls to run, without reflection.
/// </summary>
/// <remarks>
/// The publisher otherwise closes <c>INotificationHandler&lt;&gt;</c> over the notification type
/// with <c>MakeGenericType</c>, resolves the handlers as <c>object</c>, then finds and invokes
/// <c>Handle</c> through a <see cref="MethodInfo"/>. None of that survives NativeAOT, and the
/// <c>Invoke</c> costs more per handler than the handler usually does.
/// <para>
/// The generator implements this by naming every notification type it found a handler for, so each
/// entry resolves <c>GetServices&lt;INotificationHandler&lt;TNotification&gt;&gt;()</c> and calls
/// <c>Handle</c> directly - an ordinary interface call the compiler can see.
/// </para>
/// </remarks>
public interface IKyrolusNotificationDispatchSource
{
    /// <summary>
    /// One delegate per registered handler, or <see langword="null"/> if the generator never saw
    /// this notification type - which the publisher must not confuse with an empty list, since that
    /// legitimately means "no handler is registered for it".
    /// </summary>
    IReadOnlyList<Func<CancellationToken, Task>>? CreateHandlerInvocations(
        object notification,
        IServiceProvider serviceProvider);
}
