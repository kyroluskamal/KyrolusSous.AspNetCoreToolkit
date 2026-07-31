namespace KyrolusSous.Mediator.Tests;

/// <summary>
/// Builds a provider with exactly the handlers a test asks for.
/// </summary>
/// <remarks>
/// Deliberately does not scan this assembly. The assembly holds adversarial probes - handlers
/// that always throw, a behavior that short-circuits, two handlers claiming one request - which
/// exist to be opted into by one test each. Scanning would inject all of them into every test.
/// Assembly scanning itself is covered separately in <see cref="ScanningTests"/>.
/// </remarks>
internal static class TestHost
{
    /// <summary>Registers the mediator plus the handlers used by the majority of tests.</summary>
    public static IServiceCollection Standard(Recorder recorder, Action<KyrolusMediatorConfiguration>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddKyrolusMediator(configuration => configure?.Invoke(configuration));

        // Request handlers
        services.AddTransient<IKyrolusQueryHandler<Ping, string>, PingHandler>();
        services.AddTransient<IKyrolusRequestHandler<Ping, string>, PingHandler>();
        services.AddTransient<IKyrolusCommandHandler<CreateThing, Guid>, CreateThingHandler>();
        services.AddTransient<IKyrolusRequestHandler<CreateThing, Guid>, CreateThingHandler>();
        services.AddTransient<IKyrolusCommandHandler<DeleteThing>, DeleteThingHandler>();
        services.AddTransient<IKyrolusRequestHandler<FirstRequest, string>, DualRequestHandler>();
        services.AddTransient<IKyrolusRequestHandler<SecondRequest, string>, DualRequestHandler>();
        services.AddTransient<IKyrolusStreamRequestHandler<CountTo, int>, CountToHandler>();

        // Notification handlers
        services.AddTransient<INotificationHandler<SomethingHappened>, RecordingNotificationHandler>();
        services.AddTransient<INotificationHandler<SomethingHappened>, SecondRecordingNotificationHandler>();

        return services;
    }

    /// <summary>Adds the query that throws, plus its exception action and handler.</summary>
    public static IServiceCollection WithExplodingQuery(this IServiceCollection services)
    {
        services.AddTransient<IKyrolusQueryHandler<Explode, string>, ExplodeHandler>();
        services.AddTransient<IKyrolusRequestHandler<Explode, string>, ExplodeHandler>();
        services.AddTransient<IKyrolusRequestExceptionAction<Explode, InvalidOperationException>, ExplodeExceptionAction>();
        services.AddTransient<IKyrolusRequestExceptionHandler<Explode, InvalidOperationException, string>, ExplodeExceptionHandler>();
        return services;
    }

    /// <summary>Adds the pre and post processors for <see cref="Ping"/>.</summary>
    public static IServiceCollection WithPingProcessors(this IServiceCollection services)
    {
        services.AddTransient<IKyrolusRequestPreProcessor<Ping>, PingPreProcessor>();
        services.AddTransient<IKyrolusRequestPostProcessor<Ping, string>, PingPostProcessor>();
        return services;
    }
}
