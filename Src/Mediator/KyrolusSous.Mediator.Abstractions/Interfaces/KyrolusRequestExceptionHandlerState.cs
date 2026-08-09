namespace KyrolusSous.Mediator.Abstractions.Interfaces;

/// <summary>
/// The box an exception handler is handed so it can say "I recovered, use this instead".
/// </summary>
/// <remarks>
/// It exists because <c>Handle</c> returns a bare <see cref="Task"/> and so has nowhere to put a
/// replacement response. Rather than returning something, a handler writes into this object; the
/// mediator inspects it afterwards.
/// <para>
/// Both properties are read-only from the outside: the only way to fill it is
/// <see cref="SetHandled"/>, which sets the response and the flag together. That makes the
/// half-filled state - marked handled with no response, or a response nobody marked handled -
/// impossible to express.
/// </para>
/// <para>
/// Not thread-safe, and does not need to be: one instance is created per failed request and
/// exception handlers run one after another.
/// </para>
/// </remarks>
/// <typeparam name="TResponse">The response type of the request being recovered.</typeparam>
public sealed class KyrolusRequestExceptionHandlerState<TResponse>
{
    /// <summary>
    /// <see langword="true"/> once a handler has supplied a replacement response. The mediator
    /// returns <see cref="Response"/> instead of rethrowing, and skips any remaining handlers.
    /// </summary>
    public bool Handled { get; private set; }

    /// <summary>
    /// The replacement response. Only meaningful when <see cref="Handled"/> is <see langword="true"/>.
    /// </summary>
    public TResponse? Response { get; private set; }

    /// <summary>
    /// Cancels the exception and returns <paramref name="response"/> to the caller instead.
    /// </summary>
    /// <remarks>
    /// There is no undo. Once called, no further exception handler runs and the original exception
    /// is gone - so call it only when you actually have a valid answer.
    /// </remarks>
    /// <param name="response">The value to return in the exception's place.</param>
    public void SetHandled(TResponse response)
    {
        Response = response;
        Handled = true;
    }
}
