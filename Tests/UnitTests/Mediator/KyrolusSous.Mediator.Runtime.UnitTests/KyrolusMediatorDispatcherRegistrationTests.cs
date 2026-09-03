namespace KyrolusSous.Mediator.Runtime.UnitTests;

/// <summary>
/// Covers the guard that replaced a bare <c>services.Replace(...)</c> in both
/// <c>AddKyrolusMediatorGeneratedDispatcher()</c> (emitted by the generator) and
/// <c>AddKyrolusMediatorReflection()</c>: calling both for one service collection used to mean
/// whichever ran last silently discarded whatever the other had installed.
/// </summary>
public sealed class KyrolusMediatorDispatcherRegistrationTests
{
    private sealed class DummyDispatcherA : IKyrolusMediatorDispatcher
    {
        public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
        public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class DummyDispatcherB : IKyrolusMediatorDispatcher
    {
        public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
        public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
        public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct) => throw new NotSupportedException();
    }

    [Fact(DisplayName = "Install throws when a different setup method already installed the dispatcher")]
    public void Install_throws_when_a_different_setup_method_already_installed()
    {
        var services = new ServiceCollection();
        KyrolusMediatorDispatcherRegistration.Install<DummyDispatcherA>(services, "AddKyrolusMediatorGeneratedDispatcher");

        var exception = Should.Throw<InvalidOperationException>(() =>
            KyrolusMediatorDispatcherRegistration.Install<DummyDispatcherB>(services, "AddKyrolusMediatorReflection"));

        exception.Message.ShouldContain("AddKyrolusMediatorGeneratedDispatcher");
        exception.Message.ShouldContain("AddKyrolusMediatorReflection");
    }

    [Fact(DisplayName = "Install does not throw when the same setup method installs the dispatcher more than once")]
    public void Install_does_not_throw_when_called_twice_by_the_same_method()
    {
        var services = new ServiceCollection();
        KyrolusMediatorDispatcherRegistration.Install<DummyDispatcherA>(services, "AddKyrolusMediatorReflection");

        Should.NotThrow(() => KyrolusMediatorDispatcherRegistration.Install<DummyDispatcherA>(services, "AddKyrolusMediatorReflection"));

        var descriptor = services.Last(d => d.ServiceType == typeof(IKyrolusMediatorDispatcher));
        descriptor.ImplementationType.ShouldBe(typeof(DummyDispatcherA));
    }

    [Fact(DisplayName = "Install overrides a manually registered dispatcher without throwing, since that was never installed via Install")]
    public void Install_overrides_a_manually_registered_dispatcher_without_throwing()
    {
        // A test fixture (or anything else) swapping in its own IKyrolusMediatorDispatcher by hand
        // before calling the real setup method must keep working exactly as a bare Replace did -
        // the guard only ever concerns itself with the two named setup methods colliding with each
        // other, never with arbitrary code that touches the same service type.
        var services = new ServiceCollection();
        services.AddSingleton<IKyrolusMediatorDispatcher, DummyDispatcherA>();

        Should.NotThrow(() => KyrolusMediatorDispatcherRegistration.Install<DummyDispatcherB>(services, "AddKyrolusMediatorReflection"));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IKyrolusMediatorDispatcher>().ShouldBeOfType<DummyDispatcherB>();
    }

    [Fact(DisplayName = "AddKyrolusMediatorSender registers a placeholder dispatcher that throws as soon as it is constructed")]
    public void Placeholder_dispatcher_throws_on_construction()
    {
        var services = new ServiceCollection();
        services.AddKyrolusMediatorSender();

        using var provider = services.BuildServiceProvider();
        var exception = Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IKyrolusMediatorDispatcher>());
        exception.Message.ShouldContain("AddKyrolusMediatorGeneratedDispatcher");
        exception.Message.ShouldContain("AddKyrolusMediatorReflection");
    }
}
