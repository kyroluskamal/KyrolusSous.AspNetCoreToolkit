using System;
using System.Collections.Generic;

namespace KyrolusSous.Mapping.Generator.Models;

internal sealed class KyrolusPropertyMappingModel : IEquatable<KyrolusPropertyMappingModel>
{
    public string TargetPropertyName { get; set; } = string.Empty;
    public string SourcePropertyName { get; set; } = string.Empty;
    public string TargetPropertyType { get; set; } = string.Empty;
    public string SourcePropertyType { get; set; } = string.Empty;
    public bool IsDirectAssignment { get; set; }
    public bool IsNestedMapping { get; set; }
    public bool IsCollectionMapping { get; set; }
    public string? CollectionElementType { get; set; }
    public bool IsConstructorParameter { get; set; }

    public bool Equals(KyrolusPropertyMappingModel other) =>
        other is not null &&
        TargetPropertyName == other.TargetPropertyName &&
        SourcePropertyName == other.SourcePropertyName &&
        TargetPropertyType == other.TargetPropertyType &&
        SourcePropertyType == other.SourcePropertyType &&
        IsDirectAssignment == other.IsDirectAssignment &&
        IsNestedMapping == other.IsNestedMapping &&
        IsCollectionMapping == other.IsCollectionMapping &&
        IsConstructorParameter == other.IsConstructorParameter;

    public override bool Equals(object? obj) => obj is KyrolusPropertyMappingModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = TargetPropertyName.GetHashCode();
            hash = (hash * 397) ^ SourcePropertyName.GetHashCode();
            hash = (hash * 397) ^ TargetPropertyType.GetHashCode();
            hash = (hash * 397) ^ SourcePropertyType.GetHashCode();
            return hash;
        }
    }
}

internal sealed class KyrolusTypePairMappingModel : IEquatable<KyrolusTypePairMappingModel>
{
    public string SourceTypeName { get; set; } = string.Empty;
    public string TargetTypeName { get; set; } = string.Empty;
    public string SourceFullTypeName { get; set; } = string.Empty;
    public string TargetFullTypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public bool IsTargetPositionalRecord { get; set; }
    public List<KyrolusPropertyMappingModel> ConstructorParameters { get; set; } = new();
    public List<KyrolusPropertyMappingModel> Properties { get; set; } = new();

    public bool Equals(KyrolusTypePairMappingModel other) =>
        other is not null &&
        SourceFullTypeName == other.SourceFullTypeName &&
        TargetFullTypeName == other.TargetFullTypeName &&
        MethodName == other.MethodName &&
        IsTargetPositionalRecord == other.IsTargetPositionalRecord;

    public override bool Equals(object? obj) => obj is KyrolusTypePairMappingModel other && Equals(other);

    public override int GetHashCode()
    {
        unchecked
        {
            var hash = SourceFullTypeName.GetHashCode();
            hash = (hash * 397) ^ TargetFullTypeName.GetHashCode();
            hash = (hash * 397) ^ MethodName.GetHashCode();
            return hash;
        }
    }
}

internal sealed class KyrolusMapperMethodModel
{
    public string MethodName { get; set; } = string.Empty;
    public string SourceTypeName { get; set; } = string.Empty;
    public string TargetTypeName { get; set; } = string.Empty;
    public string SourceFullTypeName { get; set; } = string.Empty;
    public string TargetFullTypeName { get; set; } = string.Empty;
    public bool IsInPlace { get; set; }
    public bool IsStatic { get; set; }
    public KyrolusTypePairMappingModel TypePair { get; set; } = new();
}

internal sealed class KyrolusMapperClassModel
{
    public string Namespace { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public bool IsStatic { get; set; }
    public List<KyrolusMapperMethodModel> Methods { get; set; } = new();
}
