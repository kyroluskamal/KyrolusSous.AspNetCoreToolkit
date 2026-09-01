namespace KyrolusSous.ExceptionHandling.Runtime.Handlers;

/// <summary>
/// Catch-all handler for exceptions not matched by any more specific handler (or by any registered
/// <see cref="IKyrolusExceptionMapper"/>). Classification falls through to <see cref="Mapping.KyrolusDefaultExceptionMapper"/>,
/// which reports a generic 500 detail rather than the raw message, since an unclassified exception was never
/// designed to have its message shown to a client.
/// </summary>
public class GeneralExceptionHandler(KyrolusExceptionHandlingDependencies dependencies)
    : KyrolusExceptionHandlerBase<Exception>(dependencies);
