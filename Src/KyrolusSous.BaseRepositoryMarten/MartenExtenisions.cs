

namespace BaseRepositoryMarten;

public static class MartenExtenisions
{
    public static void AddMartenService(this IServiceCollection services, string connectionString)
    {
        services.AddMarten(options =>
        {
            options.Connection(connectionString);

            options.AutoCreateSchemaObjects = AutoCreate.All;
            options.Logger(new ConsoleMartenLogger());
            options.Events.StreamIdentity = StreamIdentity.AsString;
            options.Advanced.HiloSequenceDefaults.MaxLo = 1;
        }).UseLightweightSessions();

        services.AddScoped(sp =>
            {
                var store = sp.GetRequiredService<IDocumentStore>();
                return store.LightweightSession();
            });
    }
    public static void AddMartenService<TRegistery>(this IServiceCollection services, string connectionString)
    where TRegistery : MartenRegistry, new()
    {
        services.AddMarten(options =>
        {
            options.Connection(connectionString);

            options.AutoCreateSchemaObjects = AutoCreate.All;
            options.Logger(new ConsoleMartenLogger());
            options.Events.StreamIdentity = StreamIdentity.AsString;
            options.Advanced.HiloSequenceDefaults.MaxLo = 1;
            options.Schema.Include<TRegistery>();
        }).UseLightweightSessions();

        services.AddScoped(sp =>
            {
                var store = sp.GetRequiredService<IDocumentStore>();
                return store.LightweightSession();
            });
    }
}
