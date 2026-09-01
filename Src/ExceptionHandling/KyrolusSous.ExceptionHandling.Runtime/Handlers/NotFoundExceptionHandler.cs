namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

/// <summary>
/// Handles the toolkit's own <see cref="KyrolusNotFoundException"/> - the recommended type for "not found" errors
/// (see its own documentation for usage). Native ASP.NET Core <see cref="IExceptionHandler"/> instances are matched
/// by concrete exception type, so this targets that type directly rather than a generic placeholder.
/// </summary>
public class NotFoundExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<KyrolusNotFoundException>(dependencies);
