namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public sealed class KyrolusRequestExceptionHandlerState<TResponse>
{
    public bool Handled { get; private set; }
    public TResponse? Response { get; private set; }

    public void SetHandled(TResponse response)
    {
        Response = response;
        Handled = true;
    }
}
