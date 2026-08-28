namespace KyrolusSous.RabbitMQ.Abstractions.Evolution;

/// <summary>
/// Non-generic abstraction for event schema upcasting.
/// </summary>
public interface IKyrolusMessageUpcaster
{
    Type SourceType { get; }
    Type TargetType { get; }
    object Upcast(object oldMessage);
}

/// <summary>
/// Strongly-typed abstraction for migrating / upcasting event payloads across schema versions.
/// </summary>
/// <typeparam name="TOld">Old schema version.</typeparam>
/// <typeparam name="TNew">New schema version.</typeparam>
public interface IKyrolusMessageUpcaster<in TOld, out TNew> : IKyrolusMessageUpcaster
{
    TNew Upcast(TOld oldMessage);

    Type IKyrolusMessageUpcaster.SourceType => typeof(TOld);
    Type IKyrolusMessageUpcaster.TargetType => typeof(TNew);
    object IKyrolusMessageUpcaster.Upcast(object oldMessage) => Upcast((TOld)oldMessage)!;
}
