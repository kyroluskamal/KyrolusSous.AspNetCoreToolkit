using System.ComponentModel;

namespace KyrolusSous.Mediator.Runtime.Config;

/// <summary>
/// The placeholder <see cref="MediatorExtensions.AddKyrolusMediatorSender"/> registers for
/// <see cref="IKyrolusMediatorDispatcher"/> before either real dispatcher exists.
/// </summary>
/// <remarks>
/// Throws from its own constructor, so resolving <see cref="IKyrolusMediatorDispatcher"/> before
/// either real one is installed fails immediately with a message that says why, instead of only
/// failing once something tries to dispatch through it.
/// </remarks>
internal sealed class KyrolusMediatorDispatcherPlaceholder : IKyrolusMediatorDispatcher
{
    private const string Message =
        "[KyrolusMediator] No dispatcher is registered. Reference KyrolusSous.Mediator.Generator " +
        "and call AddKyrolusMediatorGeneratedDispatcher(), or reference " +
        "KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection().";

    public KyrolusMediatorDispatcherPlaceholder() => throw new InvalidOperationException(Message);

    public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
        => throw new InvalidOperationException(Message);

    public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct)
        => throw new InvalidOperationException(Message);

    public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
        => throw new InvalidOperationException(Message);
}

/// <summary>
/// Records which of <c>AddKyrolusMediatorGeneratedDispatcher()</c> and
/// <c>AddKyrolusMediatorReflection()</c> last installed the dispatcher, so
/// <see cref="KyrolusMediatorDispatcherRegistration.Install{TDispatcher}"/> can tell the two of them
/// colliding apart from anything else touching <see cref="IKyrolusMediatorDispatcher"/>.
/// </summary>
/// <param name="MethodName">The setup method that installed the dispatcher.</param>
internal sealed record KyrolusMediatorDispatcherInstallMarker(string MethodName);

/// <summary>
/// The only way <c>AddKyrolusMediatorGeneratedDispatcher()</c> (emitted by
/// <c>KyrolusSous.Mediator.Generator</c>) and <c>AddKyrolusMediatorReflection()</c> install their
/// dispatcher.
/// </summary>
/// <remarks>
/// Both used to call <c>services.Replace(...)</c> directly, which - contrary to what
/// <see cref="IKyrolusMediatorDispatcher"/>'s own remarks used to claim - means whichever is called
/// <em>last</em> wins, silently, with no diagnostic. An application that referenced both packages
/// and, for whatever reason (a shared test-setup helper calling
/// <c>AddKyrolusMediatorReflection()</c> after production start-up code already called
/// <c>AddKyrolusMediatorGeneratedDispatcher()</c>, say), ended up calling both, would lose the
/// generated, NativeAOT-safe dispatcher without any warning - the application would build and run,
/// and only fail the moment NativeAOT actually needed the code path the reflection dispatcher
/// cannot provide.
/// <para>
/// The check is deliberately narrow: it only ever compares against
/// <see cref="KyrolusMediatorDispatcherInstallMarker"/>, a marker only these two setup methods ever
/// write - never against whatever is currently registered for <see cref="IKyrolusMediatorDispatcher"/>
/// itself. Code that registers its own <see cref="IKyrolusMediatorDispatcher"/> by hand (a test
/// fixture swapping in a mock before calling the real setup, for instance) is untouched by this
/// guard and still gets overridden exactly as a bare <c>Replace</c> would have done; only the two
/// named setup methods colliding with <em>each other</em> is treated as a mistake worth failing on.
/// </para>
/// <para>
/// Installing the same one twice is still fine - calling either setup method more than once must
/// not become an error - but installing a <em>different</em> one after the other setup method
/// already ran throws immediately, naming both so the mistake is obvious at start-up instead of at
/// first request.
/// </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class KyrolusMediatorDispatcherRegistration
{
    /// <summary>
    /// Installs <typeparamref name="TDispatcher"/> as the <see cref="IKyrolusMediatorDispatcher"/>,
    /// unless the other setup method already installed a different one.
    /// </summary>
    /// <param name="services">The service collection being configured.</param>
    /// <param name="methodName">
    /// The name of the calling setup method - recorded in the marker, and used to name it in the
    /// exception message when the two collide.
    /// </param>
    /// <exception cref="InvalidOperationException">
    /// The other setup method already installed the dispatcher for this service collection - both
    /// <c>AddKyrolusMediatorGeneratedDispatcher()</c> and <c>AddKyrolusMediatorReflection()</c> were
    /// called.
    /// </exception>
    public static void Install<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TDispatcher>(
        IServiceCollection services,
        string methodName)
        where TDispatcher : class, IKyrolusMediatorDispatcher
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(methodName);

        var marker = FindMarker(services);
        if (marker is not null && marker.MethodName != methodName)
        {
            throw new InvalidOperationException(
                $"[KyrolusMediator] {methodName}() was called, but {marker.MethodName}() was already " +
                "called for this service collection and installed its own dispatcher. Call exactly " +
                "one of AddKyrolusMediatorGeneratedDispatcher() or AddKyrolusMediatorReflection() - " +
                "never both.");
        }

        if (marker is null)
            services.AddSingleton(new KyrolusMediatorDispatcherInstallMarker(methodName));

        services.Replace(ServiceDescriptor.Singleton<IKyrolusMediatorDispatcher, TDispatcher>());
    }

    private static KyrolusMediatorDispatcherInstallMarker? FindMarker(IServiceCollection services)
    {
        for (var i = services.Count - 1; i >= 0; i--)
            if (services[i].ServiceType == typeof(KyrolusMediatorDispatcherInstallMarker)
                && services[i].ImplementationInstance is KyrolusMediatorDispatcherInstallMarker marker)
                return marker;
        return null;
    }
}
