using KyrolusSous.CQRS.Marten.Saga;
using KyrolusSous.CQRS.Saga;

namespace KyrolusSous.CQRS.Marten.Config;

/// <summary>
/// Registers <see cref="MartenSagaStore"/> as the application's <see cref="IKyrolusSagaStore"/>.
/// </summary>
public static class KyrolusMartenSagaStoreServiceCollectionExtensions
{
    /// <summary>
    /// Replaces the default in-memory saga store (see <c>AddKyrolusCqrsSaga</c>) with a Marten-backed
    /// one - use this for any deployment where a saga must survive a process restart. Requires nothing
    /// further from the consuming application: <see cref="MartenSagaStore"/> depends only on
    /// <see cref="IDocumentStore"/>, which <c>services.AddMarten(...)</c> already registers, and its
    /// document type needs no fluent <c>StoreOptions</c> configuration - see
    /// <see cref="KyrolusMartenSagaInstanceDocument"/>'s remarks.
    /// </summary>
    /// <param name="martenConfiguration">The expression returned by <c>services.AddMarten(...)</c>.</param>
    /// <returns>The same <paramref name="martenConfiguration"/>, for chaining.</returns>
    public static global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression UseKyrolusMartenSagaStore(
        this global::Marten.MartenServiceCollectionExtensions.MartenConfigurationExpression martenConfiguration)
    {
        ArgumentNullException.ThrowIfNull(martenConfiguration);

        martenConfiguration.Services.AddKyrolusSagaStore<MartenSagaStore>();
        return martenConfiguration;
    }
}
