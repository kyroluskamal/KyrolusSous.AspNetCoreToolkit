using System.Reflection;

namespace KyrolusSous.CQRS.Elasticsearch.Command;

/// <summary>
/// Generic CQRS command performing in-place partial updates to specific fields of an Elasticsearch document.
/// </summary>
/// <typeparam name="TDocument">The document model type indexed in Elasticsearch.</typeparam>
/// <typeparam name="TId">The document identifier type.</typeparam>
/// <remarks>
/// <see cref="PartialDocument"/> is typically built directly from a PATCH request body, so it is written via
/// reflection/dictionary keys with no column-level authorization of its own. Implementing
/// <see cref="IKyrolusPropertyUpdateRequest"/> lets the already-registered
/// <c>KyrolusPropertyAllowListBehavior</c> pipeline behavior (see its own remarks, ordered at -940) reject a
/// disallowed property name before it ever reaches the handler - the same opt-in guard the EF and Marten
/// Patch/ExecuteUpdate commands use. <see cref="AllowedProperties"/> defaults to <see langword="null"/>
/// (unrestricted), so existing callers who never set it keep writing whatever property names they always
/// could.
/// </remarks>
public sealed record ElasticUpdatePartialCommand<TDocument, TId>(
    TId Id,
    object PartialDocument) : IKyrolusCommand<bool>, IKyrolusPropertyUpdateRequest
    where TDocument : class
{
    /// <summary>Optional expected sequence number for an Elasticsearch optimistic-concurrency check (see <see cref="ExpectedPrimaryTerm"/>). Both must be supplied together, or neither - a lone value is rejected by Elasticsearch itself.</summary>
    public long? ExpectedSeqNo { get; init; }

    /// <summary>Optional expected primary term for an Elasticsearch optimistic-concurrency check (see <see cref="ExpectedSeqNo"/>). Both must be supplied together, or neither.</summary>
    public long? ExpectedPrimaryTerm { get; init; }

    /// <inheritdoc cref="IKyrolusPropertyUpdateRequest.AllowedProperties"/>
    public IReadOnlySet<string>? AllowedProperties { get; init; }

    /// <summary>
    /// Every property name <see cref="PartialDocument"/> would write. When it is a dictionary (the common
    /// shape for a PATCH body deserialized as <c>Dictionary&lt;string, object&gt;</c>), its keys are used
    /// directly; otherwise every public instance property name on <see cref="PartialDocument"/>'s runtime
    /// type is used, which covers the common "anonymous object" partial-update usage (<c>new { Name = "x" }</c>).
    /// </summary>
    IEnumerable<string> IKyrolusPropertyUpdateRequest.UpdatedPropertyNames
    {
        get
        {
            if (PartialDocument is System.Collections.IDictionary dictionary)
            {
                foreach (var key in dictionary.Keys)
                {
                    if (key is string name)
                    {
                        yield return name;
                    }
                }

                yield break;
            }

            foreach (var property in PartialDocument.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return property.Name;
            }
        }
    }
}
