using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Mediator.Abstractions.Compatibility;

/// <summary>
/// MediatR's <c>IBaseRequest</c>. Non-generic marker on every request.
/// </summary>
/// <remarks>
/// <para>
/// Everything in this namespace is an alias: a type carrying MediatR's name that inherits the
/// Kyrolus equivalent. Nothing here adds behaviour. The point is that code written against
/// MediatR compiles after changing one line - <c>using MediatR;</c> becomes
/// <c>using KyrolusSous.Mediator.Abstractions.Compatibility;</c> - instead of being rewritten.
/// </para>
/// <para>
/// Constraints deliberately mirror MediatR's rather than the Kyrolus ones. A tighter constraint
/// here would reject code that compiled fine before the move, which defeats the purpose.
/// </para>
/// <para>
/// Method names are covered separately by <see cref="MediatRMethodAliases"/>, which adds
/// <c>Send</c> / <c>Publish</c> / <c>CreateStream</c> alongside the <c>...Async</c> names.
/// </para>
/// <para>
/// For new code, prefer the Kyrolus interfaces directly: they distinguish commands from queries,
/// which these do not.
/// </para>
/// </remarks>
public interface IBaseRequest : IKyrolusRequestBase { }

/// <summary>
/// MediatR's <c>IRequest&lt;TResponse&gt;</c>: a message handled by exactly one handler.
/// Equivalent to <see cref="IKyrolusRequest{TResponse}"/>.
/// </summary>
/// <remarks>
/// MediatR has no command/query distinction, so a ported request is neither. It still works -
/// the dispatcher falls back to <see cref="IKyrolusRequestHandler{TRequest, TResponse}"/> - but
/// behaviors that key off <see cref="IKyrolusQueryBase"/>, caching in particular, will skip it.
/// Re-declare it as <see cref="IKyrolusQuery{TResponse}"/> or <see cref="IKyrolusCommand{TResponse}"/>
/// to opt in.
/// </remarks>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface IRequest<out TResponse> : IKyrolusRequest<TResponse>, IBaseRequest { }

/// <summary>
/// MediatR's <c>IRequest</c>: a message that produces no value.
/// Equivalent to <see cref="IKyrolusCommand"/>.
/// </summary>
public interface IRequest : IKyrolusRequest<Unit>, IBaseRequest { }

/// <summary>
/// MediatR's <c>IRequestHandler&lt;TRequest, TResponse&gt;</c>.
/// Equivalent to <see cref="IKyrolusRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The type produced by the handler.</typeparam>
public interface IRequestHandler<in TRequest, TResponse> : IKyrolusRequestHandler<TRequest, TResponse>
    where TRequest : IRequest<TResponse> { }

/// <summary>
/// MediatR's <c>IRequestHandler&lt;TRequest&gt;</c> for requests that produce no value.
/// Equivalent to <see cref="IKyrolusRequestHandler{TRequest}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<in TRequest> : IKyrolusRequestHandler<TRequest>
    where TRequest : IRequest { }

/// <summary>
/// MediatR's <c>IStreamRequest&lt;TResponse&gt;</c>.
/// Equivalent to <see cref="IKyrolusStreamRequest{TResponse}"/>.
/// </summary>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
public interface IStreamRequest<out TResponse> : IKyrolusStreamRequest<TResponse> { }

/// <summary>
/// MediatR's <c>IStreamRequestHandler&lt;TRequest, TResponse&gt;</c>.
/// Equivalent to <see cref="IKyrolusStreamRequestHandler{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The stream request type.</typeparam>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
public interface IStreamRequestHandler<in TRequest, TResponse> : IKyrolusStreamRequestHandler<TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse> { }

/// <summary>
/// MediatR's <c>IPipelineBehavior&lt;TRequest, TResponse&gt;</c>.
/// Equivalent to <see cref="IKyrolusPipelineBehavior{TRequest, TResponse}"/>.
/// </summary>
/// <remarks>
/// MediatR constrains only <c>TRequest : notnull</c>, and so does this. An earlier
/// <c>where TRequest : IKyrolusRequest&lt;TResponse&gt;</c> made ported behaviors fail to compile,
/// because MediatR behaviors are routinely written open over any request type.
/// </remarks>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse> : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull { }

/// <summary>
/// MediatR's <c>IStreamPipelineBehavior&lt;TRequest, TResponse&gt;</c>.
/// Equivalent to <see cref="IKyrolusStreamPipelineBehavior{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The stream request type.</typeparam>
/// <typeparam name="TResponse">The type of a single streamed item.</typeparam>
public interface IStreamPipelineBehavior<in TRequest, TResponse> : IKyrolusStreamPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull { }

/// <summary>
/// MediatR's <c>IRequestPreProcessor&lt;TRequest&gt;</c>: runs before the handler.
/// Equivalent to <see cref="IKyrolusRequestPreProcessor{TRequest}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestPreProcessor<in TRequest> : IKyrolusRequestPreProcessor<TRequest>
    where TRequest : notnull { }

/// <summary>
/// MediatR's <c>IRequestPostProcessor&lt;TRequest, TResponse&gt;</c>: runs after the handler.
/// Equivalent to <see cref="IKyrolusRequestPostProcessor{TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestPostProcessor<in TRequest, in TResponse> : IKyrolusRequestPostProcessor<TRequest, TResponse>
    where TRequest : notnull { }

/// <summary>
/// MediatR's <c>IRequestExceptionHandler</c>: recovers from an exception with a replacement response.
/// Equivalent to <see cref="IKyrolusRequestExceptionHandler{TRequest, TException, TResponse}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TException">The exception type to recover from.</typeparam>
/// <typeparam name="TResponse">The response type of the request.</typeparam>
public interface IRequestExceptionHandler<in TRequest, in TException, TResponse>
    : IKyrolusRequestExceptionHandler<TRequest, TException, TResponse>
    where TRequest : notnull
    where TException : Exception { }

/// <summary>
/// MediatR's <c>IRequestExceptionAction</c>: reacts to an exception without stopping it.
/// Equivalent to <see cref="IKyrolusRequestExceptionAction{TRequest, TException}"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TException">The exception type to react to.</typeparam>
public interface IRequestExceptionAction<in TRequest, in TException>
    : IKyrolusRequestExceptionAction<TRequest, TException>
    where TRequest : notnull
    where TException : Exception { }

/// <summary>
/// MediatR's <c>INotificationPublisher</c>: decides how notification handlers are scheduled.
/// Equivalent to <see cref="IKyrolusNotificationPublishStrategy"/>.
/// </summary>
public interface INotificationPublisher : IKyrolusNotificationPublishStrategy { }

/// <summary>
/// MediatR's <c>ISender</c>: the send half of the mediator.
/// Equivalent to <see cref="IKyrolusMediatorSender"/>.
/// </summary>
public interface ISender : IKyrolusMediatorSender { }

/// <summary>
/// MediatR's <c>IPublisher</c>: the publish half of the mediator.
/// Equivalent to <see cref="IKyrolusMediatorPublisher"/>.
/// </summary>
public interface IPublisher : IKyrolusMediatorPublisher { }

/// <summary>
/// MediatR's <c>IMediator</c>. Equivalent to <see cref="IKyrolusMediator"/>, and registered
/// against the same implementation - resolving either gives the same mediator.
/// </summary>
public interface IMediator : IKyrolusMediator { }
