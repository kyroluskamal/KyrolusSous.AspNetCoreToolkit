namespace KyrolusSous.Elasticsearch;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticTextAttribute : Attribute
{
    public string Analyzer { get; set; } = "standard";

    public string? SearchAnalyzer { get; set; }

    public bool Index { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticKeywordAttribute : Attribute
{
    public bool Index { get; set; } = true;

    public bool IgnoreAbove { get; set; } = false;

    public int MaxLength { get; set; } = 256;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticGeoPointAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticDenseVectorAttribute(int dimensions = 1536) : Attribute
{
    public int Dimensions { get; set; } = dimensions;

    public string Similarity { get; set; } = "cosine";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SyncToElasticsearchAttribute : Attribute
{
    public string? IndexName { get; set; }

    public string IdProperty { get; set; } = "Id";
}
