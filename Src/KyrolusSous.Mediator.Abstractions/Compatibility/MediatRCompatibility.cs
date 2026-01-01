using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Mediator.Abstractions.Compatibility;

/// <summary>
/// MediatR-style compatibility interfaces that map to Kyrolus mediator abstractions.
/// </summary>
public interface IRequest<out TResponse> : IKyrolusRequest<TResponse> { }

public interface IRequest : IKyrolusRequest<Unit> { }

public interface IRequestHandler<in TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : notnull { }

public interface IStreamRequest<out TResponse> : IKyrolusStreamRequest<TResponse> { }

public interface IStreamRequestHandler<in TRequest, TResponse> : IKyrolusStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse> { }

public interface IPipelineBehavior<in TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TRequest : IKyrolusRequest<TResponse> { }

public interface IMediator : IKyrolusMediator { }
