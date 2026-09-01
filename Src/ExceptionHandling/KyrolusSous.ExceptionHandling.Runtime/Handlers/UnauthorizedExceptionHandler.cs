namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

/// <summary>
/// Handles the toolkit's own <see cref="KyrolusUnauthorizedException"/> - the recommended type for authentication
/// failures (see its own documentation for usage). Native ASP.NET Core <see cref="IExceptionHandler"/> instances
/// are matched by concrete exception type, so this targets that type directly rather than a generic placeholder.
/// </summary>
public class UnauthorizedExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<KyrolusUnauthorizedException>(dependencies);
