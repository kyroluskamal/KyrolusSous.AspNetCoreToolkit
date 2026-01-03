namespace KyrolusSous.Validation.Abstractions;

public static class KyrolusValidationComposite
{
    public static KyrolusValidationComposite<TFirst, TSecond> Create<TFirst, TSecond>(TFirst first, TSecond second)
        => new(first, second);

    public static KyrolusValidationComposite<TFirst, TSecond, TThird> Create<TFirst, TSecond, TThird>(
        TFirst first,
        TSecond second,
        TThird third)
        => new(first, second, third);

    public static KyrolusValidationComposite<TFirst, TSecond, TThird, TFourth> Create<TFirst, TSecond, TThird, TFourth>(
        TFirst first,
        TSecond second,
        TThird third,
        TFourth fourth)
        => new(first, second, third, fourth);
}

public sealed record KyrolusValidationComposite<TFirst, TSecond>(TFirst First, TSecond Second);

public sealed record KyrolusValidationComposite<TFirst, TSecond, TThird>(TFirst First, TSecond Second, TThird Third);

public sealed record KyrolusValidationComposite<TFirst, TSecond, TThird, TFourth>(
    TFirst First,
    TSecond Second,
    TThird Third,
    TFourth Fourth);
