namespace KyrolusSous.CQRS.Marten;

/// <summary>
/// Distinguishes "optional repository not registered" from any other <see cref="InvalidOperationException"/>
/// when probing for a soft-delete repository a caller may not have configured.
/// </summary>
/// <remarks>
/// <c>IKyrolusMartenUnitOfWork&lt;TSession&gt;.GetRepository&lt;TRepo&gt;()</c> throws
/// <see cref="InvalidOperationException"/> with a message containing "registered" specifically when
/// nothing is registered for <c>TRepo</c> - the runtime unit of work says so explicitly
/// ("...is not registered."), and the source-generated one delegates to
/// <c>IServiceProvider.GetRequiredService&lt;TRepo&gt;()</c>, whose own message ("No service for type
/// '...' has been registered.") also contains the word. A handler probing for an optional
/// soft-delete repository used to catch every <see cref="InvalidOperationException"/> unconditionally
/// and treat it as "not configured", falling back to non-soft-delete behavior. But
/// <c>IKyrolusMartenUnitOfWork&lt;TSession&gt;</c> is a public interface: a caller-supplied
/// implementation, or a custom <c>repositoryFactory</c> delegate passed into the runtime unit of
/// work, can throw <see cref="InvalidOperationException"/> for a completely unrelated reason (a
/// disposed session, a misconfigured factory) - swallowing that unconditionally would silently
/// misreport a real failure as "this feature just isn't configured".
/// </remarks>
internal static class KyrolusRepositoryResolution
{
    public static bool IsRepositoryNotRegistered(this InvalidOperationException exception)
        => exception.Message.Contains("registered", StringComparison.OrdinalIgnoreCase);
}
