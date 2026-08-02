using System.ComponentModel;
using KyrolusSous.Mediator.Runtime.Internal;

namespace KyrolusSous.Mediator.Runtime.GeneratorIntegration;

/// <summary>
/// Supplies pipeline wrappers already closed over their request and response types.
/// </summary>
/// <remarks>
/// Without this the sender builds each wrapper with <c>MakeGenericType</c>, which NativeAOT cannot
/// always satisfy. Reference-type arguments share one canonical instantiation and survive, but
/// <c>RequestPipelineWrapperImpl&lt;GetCount, int&gt;</c> needs code the compiler emits only for
/// instantiations it can see statically - and nothing reaches that one except the reflection call
/// itself, so the request fails when it is sent rather than when the app is built.
/// <para>
/// The generator implements this interface by naming every (request, response) pair it found, so
/// each instantiation becomes a plain static call the compiler can see. An application that does
/// not use the generator registers no implementation, and the sender keeps its reflection path.
/// </para>
/// </remarks>
public interface IKyrolusPipelineWrapperSource
{
    /// <summary>
    /// The request wrapper for the pair, or <see langword="null"/> if the generator did not see it -
    /// an open generic handler closed only at the call site, for instance.
    /// </summary>
    object? CreateRequestWrapper(Type requestType, Type responseType);

    /// <summary>The stream wrapper for the pair, or <see langword="null"/> if it was not generated.</summary>
    object? CreateStreamWrapper(Type requestType, Type responseType);

    /// <summary>
    /// The response a request declares, for the overloads that take <see cref="object"/> and so
    /// cannot learn it from the call site.
    /// </summary>
    /// <remarks>
    /// Reading it off the request type means <c>GetInterfaces</c>, which trimming is free to break.
    /// Asking the source instead keeps the sender clear of reflection entirely.
    /// </remarks>
    /// <param name="requestType">The concrete request type.</param>
    /// <param name="stream">
    /// <see langword="true"/> to read <c>IKyrolusStreamRequest&lt;&gt;</c> rather than
    /// <c>IKyrolusRequest&lt;&gt;</c>.
    /// </param>
    /// <returns>
    /// The response type, or <see langword="null"/> when the source cannot answer - the request was
    /// not seen, or it declares more than one response and the answer would be a guess.
    /// </returns>
    Type? GetResponseType(Type requestType, bool stream);
}

/// <summary>
/// The only way generated code can construct a pipeline wrapper.
/// </summary>
/// <remarks>
/// The wrapper types are internal, and widening them to public would put an implementation detail
/// into the public API for the sake of one call. These two methods are the seam instead: generated
/// code calls them with concrete type arguments, which is exactly what makes the result
/// AOT-safe - there is no <c>MakeGenericType</c> anywhere in the path.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class KyrolusPipelineWrapperFactory
{
    /// <summary>Creates the request pipeline wrapper for one (request, response) pair.</summary>
    public static object CreateRequest<TRequest, TResponse>()
        => new RequestPipelineWrapperImpl<TRequest, TResponse>();

    /// <summary>Creates the stream pipeline wrapper for one (request, response) pair.</summary>
    public static object CreateStream<TRequest, TResponse>()
        => new StreamPipelineWrapperImpl<TRequest, TResponse>();
}
