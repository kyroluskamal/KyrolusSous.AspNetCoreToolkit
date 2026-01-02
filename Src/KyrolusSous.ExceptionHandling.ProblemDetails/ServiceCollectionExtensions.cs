global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KyrolusSous.ExceptionHandling.ProblemDetails;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusProblemDetailsWriter(this IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IKyrolusErrorResponseWriter, KyrolusProblemDetailsWriter>());
        return services;
    }
}
