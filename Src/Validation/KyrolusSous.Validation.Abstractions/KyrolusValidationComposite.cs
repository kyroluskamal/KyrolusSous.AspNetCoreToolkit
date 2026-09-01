namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Factory helpers for instantiating strongly-typed validation composite tuples.
/// </summary>
public static class KyrolusValidationComposite
{
    /// <summary>Creates a composite validating two objects concurrently.</summary>
    public static KyrolusValidationComposite<TFirst, TSecond> Create<TFirst, TSecond>(TFirst first, TSecond second)
        => new(first, second);

    /// <summary>Creates a composite validating three objects concurrently.</summary>
    public static KyrolusValidationComposite<TFirst, TSecond, TThird> Create<TFirst, TSecond, TThird>(
        TFirst first, TSecond second, TThird third) => new(first, second, third);

    /// <summary>Creates a composite validating four objects concurrently.</summary>
    public static KyrolusValidationComposite<TFirst, TSecond, TThird, TFourth> Create<TFirst, TSecond, TThird, TFourth>(
        TFirst first, TSecond second, TThird third, TFourth fourth)
        => new(first, second, third, fourth);
}

/// <summary>Represents a tuple of 2 models to validate simultaneously.</summary>
public sealed record KyrolusValidationComposite<TFirst, TSecond>(TFirst First, TSecond Second);

/// <summary>Represents a tuple of 3 models to validate simultaneously.</summary>
public sealed record KyrolusValidationComposite<TFirst, TSecond, TThird>(TFirst First, TSecond Second, TThird Third);

/// <summary>Represents a tuple of 4 models to validate simultaneously.</summary>
public sealed record KyrolusValidationComposite<TFirst, TSecond, TThird, TFourth>(
    TFirst First, TSecond Second, TThird Third, TFourth Fourth);
