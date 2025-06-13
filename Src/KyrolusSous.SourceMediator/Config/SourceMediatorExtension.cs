using KyrolusSous.SourceMediator.Implementations;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.SourceMediator.Config;

public static class MediatorExtensions
{
    public static void AddSourceMediatorSender(this IServiceCollection services)
    {
        services.TryAddScoped<ISourceSender, SourceSender>();
    }

    public static void AddSourceMediatorPublisher(this IServiceCollection services)
    {
        services.TryAddScoped<ISourcePublisher, SourcePublisher>();
    }

    public static void AddSourceMediator(this IServiceCollection services)
    {
        services.AddSourceMediatorSender();
        services.AddSourceMediatorPublisher();
    }
}

