namespace KyrolusSous.Elasticsearch;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticTextAttribute : Attribute
{
    public string Analyzer { get; set; } = "standard";

    public string? SearchAnalyzer { get; set; }

    public bool Index { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticKeywordAttribute : Attribute
{
    public bool Index { get; set; } = true;

    public bool IgnoreAbove { get; set; } = false;

    public int MaxLength { get; set; } = 256;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticGeoPointAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticDenseVectorAttribute(int dimensions = 1536) : Attribute
{
    public int Dimensions { get; set; } = dimensions;

    public string Similarity { get; set; } = "cosine";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusSyncToElasticsearchAttribute : Attribute
{
    public string? IndexName { get; set; }

    public string IdProperty { get; set; } = "Id";
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticNestedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticDateAttribute : Attribute
{
    public string? Format { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticCompletionAttribute : Attribute
{
    public string Analyzer { get; set; } = "simple";

    public string? SearchAnalyzer { get; set; }

    public bool PreserveSeparators { get; set; } = true;

    public bool PreservePositionIncrements { get; set; } = true;

    public int MaxInputLength { get; set; } = 50;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class KyrolusElasticPercolatorAttribute : Attribute
{
}
