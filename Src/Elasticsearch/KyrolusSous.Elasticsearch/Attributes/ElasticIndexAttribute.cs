namespace KyrolusSous.Elasticsearch;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ElasticIndexAttribute(string indexName) : Attribute
{
    public string IndexName { get; } = indexName;

    public int NumberOfShards { get; set; } = 1;

    public int NumberOfReplicas { get; set; } = 1;

    public string? Alias { get; set; }

    public bool UseAlias { get; set; } = false;
}
