namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Configures Elasticsearch index properties for a document model.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticIndexAttribute(string indexName) : Attribute
{
    public string IndexName { get; } = indexName;

    public int NumberOfShards { get; set; } = 1;

    public int NumberOfReplicas { get; set; } = 1;

    public string? Alias { get; set; }

    public bool UseAlias { get; set; } = false;

    public string? IlmPolicyName { get; set; }
}

/// <summary>
/// Backward-compatibility alias for <see cref="KyrolusElasticIndexAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ElasticIndexAttribute(string indexName) : KyrolusElasticIndexAttribute(indexName)
{
}
