

namespace KyrolusSous.BaseConfig.Config;

public static class SwaggerServiceExtension
{
    public static void AddSwaggerService(this IServiceCollection services, string title, string version = "v1")
    {
        services.AddEndpointsApiExplorer();

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc(version, new OpenApiInfo { Title = title, Version = version });

        }).AddEndpointsApiExplorer();
    }

    public static void UseConfiguredSwaggerUI(this WebApplication app, Action<SwaggerUIOptions>? configureOptions = null)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                configureOptions?.Invoke(options);
            });
        }
    }
}
