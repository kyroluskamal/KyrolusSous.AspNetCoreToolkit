using KyrolusSous.Mediator.Abstractions.Interfaces;

namespace KyrolusSous.Mediator.Abstractions.Compatibility;

/// <summary>
/// MediatR's method names, forwarding to the Kyrolus ones.
/// </summary>
/// <remarks>
/// The interfaces in the MediatRCompatibility namespace let ported type declarations compile
/// unchanged, but every call site still had to be edited because MediatR calls the methods
/// <c>Send</c> / <c>Publish</c> / <c>CreateStream</c> while these are <c>SendAsync</c> /
/// <c>PublishAsync</c> / <c>StreamAsync</c>. Adding a <c>using</c> for this namespace makes those
/// call sites compile too.
/// <para>
/// These are extension methods rather than interface members so that adding them breaks nobody:
/// every existing implementation of <see cref="IKyrolusMediator"/> keeps compiling. The
/// trade-off is that a mocking library cannot intercept them - a test that needs to fake the
/// mediator should set up the underlying <c>...Async</c> method instead.
/// </para>
/// </remarks>
public static class MediatRMethodAliases
{
    /// <summary>MediatR's name for <see cref="IKyrolusMediatorSender.SendAsync{TResponse}(IKyrolusRequest{TResponse}, CancellationToken)"/>.</summary>
    public static Task<TResponse> Send<TResponse>(
        this IKyrolusMediatorSender sender,
        IKyrolusRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.SendAsync(request, cancellationToken);
    }

    /// <summary>MediatR's name for sending a request that produces no value.</summary>
    public static Task Send<TRequest>(
        this IKyrolusMediatorSender sender,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : IKyrolusRequest<Unit>
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.SendAsync(request, cancellationToken);
    }

    /// <summary>MediatR's name for sending a request whose type is only known at runtime.</summary>
    public static Task<object?> Send(
        this IKyrolusMediatorSender sender,
        object request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.SendAsync(request, cancellationToken);
    }

    /// <summary>MediatR's name for <see cref="IKyrolusMediatorSender.StreamAsync{TResponse}(IKyrolusStreamRequest{TResponse}, CancellationToken)"/>.</summary>
    public static IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        this IKyrolusMediatorSender sender,
        IKyrolusStreamRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.StreamAsync(request, cancellationToken);
    }

    /// <summary>MediatR's name for streaming a request whose type is only known at runtime.</summary>
    public static IAsyncEnumerable<object?> CreateStream(
        this IKyrolusMediatorSender sender,
        object request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sender);
        return sender.StreamAsync(request, cancellationToken);
    }

    /// <summary>MediatR's name for <see cref="IKyrolusMediatorPublisher.PublishAsync(IKyrolusNotification, CancellationToken)"/>.</summary>
    public static Task Publish<TNotification>(
        this IKyrolusMediatorPublisher publisher,
        TNotification notification,
        CancellationToken cancellationToken = default)
        where TNotification : IKyrolusNotification
    {
        ArgumentNullException.ThrowIfNull(publisher);
        return publisher.PublishAsync(notification, cancellationToken);
    }

    /// <summary>MediatR's name for publishing a notification whose type is only known at runtime.</summary>
    public static Task Publish(
        this IKyrolusMediatorPublisher publisher,
        object notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(publisher);
        return publisher.PublishAsync(notification, cancellationToken);
    }
}
