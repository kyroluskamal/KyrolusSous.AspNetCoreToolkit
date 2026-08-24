namespace KyrolusSous.Repositories.EF.Abstractions.Dynamic;

/// <summary>
/// Specifies the sorting direction for dynamic string queries.
/// </summary>
public enum KyrolusSortDirection
{
    /// <summary>
    /// Ascending order.
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending order.
    /// </summary>
    Descending
}

/// <summary>
/// Represents a parsed single-field sort descriptor.
/// </summary>
/// <param name="PropertyName">The name of the property.</param>
/// <param name="Direction">The sorting direction.</param>
public readonly record struct KyrolusSortField(string PropertyName, KyrolusSortDirection Direction);

/// <summary>
/// Represents comparison operators supported in dynamic string filters.
/// </summary>
public enum KyrolusFilterOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Contains,
    StartsWith,
    EndsWith
}
