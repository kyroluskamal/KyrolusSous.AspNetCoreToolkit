namespace KyrolusSous.ExceptionHandling.Runtime;

/// <summary>
/// ASP.NET Core-specific extensions for <see cref="KyrolusExceptionHandlingOptions"/>. Kept separate from the
/// options class itself (which lives in Abstractions and stays framework-agnostic) since ASP.NET Core-only
/// exception types like <see cref="BadHttpRequestException"/> aren't available there.
/// </summary>
public static class KyrolusExceptionHandlingOptionsExtensions
{
    /// <summary>
    /// Suppresses server logging for noisy ASP.NET Core request-parsing failures (<see cref="BadHttpRequestException"/>),
    /// on top of the generic cancellation exceptions already covered by <see cref="KyrolusExceptionHandlingOptions.IgnoreCommonNoisyExceptions"/>.
    /// </summary>
    /// <returns>The current options instance for chaining.</returns>
    public static KyrolusExceptionHandlingOptions IgnoreCommonAspNetCoreNoisyExceptions(this KyrolusExceptionHandlingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.IgnoredExceptionLogTypes.Add(typeof(BadHttpRequestException));
        return options;
    }
}
