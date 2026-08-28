namespace KyrolusSous.Elasticsearch;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticTextAttribute : Attribute
{
    public string Analyzer { get; set; } = "standard";

    public string? SearchAnalyzer { get; set; }

    public bool Index { get; set; } = true;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticTextAttribute : KyrolusElasticTextAttribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticKeywordAttribute : Attribute
{
    public bool Index { get; set; } = true;

    public bool IgnoreAbove { get; set; } = false;

    public int MaxLength { get; set; } = 256;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticKeywordAttribute : KyrolusElasticKeywordAttribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticGeoPointAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticGeoPointAttribute : KyrolusElasticGeoPointAttribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticDenseVectorAttribute(int dimensions = 1536) : Attribute
{
    public int Dimensions { get; set; } = dimensions;

    public string Similarity { get; set; } = "cosine";
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticDenseVectorAttribute(int dimensions = 1536) : KyrolusElasticDenseVectorAttribute(dimensions)
{
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class KyrolusSyncToElasticsearchAttribute : Attribute
{
    public string? IndexName { get; set; }

    public string IdProperty { get; set; } = "Id";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class SyncToElasticsearchAttribute : KyrolusSyncToElasticsearchAttribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticNestedAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticNestedAttribute : KyrolusElasticNestedAttribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public class KyrolusElasticDateAttribute : Attribute
{
    public string? Format { get; set; }
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
public sealed class ElasticDateAttribute : KyrolusElasticDateAttribute
{
}
