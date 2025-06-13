namespace KyrolusSous.ExceptionHandling;

public static class ExceptionHandlingExtension
{
    public static void AddExceptionHandlers(this IServiceCollection services)
    {
        services.AddExceptionHandler<ValidationExceptionHandler>();
        services.AddExceptionHandler<NotFoundExceptionHandler>();
        services.AddExceptionHandler<NpgsqlExceptionHandler>();
        services.AddExceptionHandler<AuthenticationExceptionHandler>();
        services.AddExceptionHandler<UnauthorizedExceptionHandler>();
        services.AddExceptionHandler<GeneralExceptionHandler>();
    }
}

