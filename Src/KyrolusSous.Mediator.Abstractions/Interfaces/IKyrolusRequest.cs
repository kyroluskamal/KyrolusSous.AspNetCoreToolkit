namespace KyrolusSous.Mediator.Abstractions.Interfaces;

public interface IKyrolusRequest<out TResponse>
{
}

public interface IKyrolusRequest : IKyrolusRequest<Unit>
{
}
