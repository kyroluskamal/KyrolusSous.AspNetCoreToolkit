namespace KyrolusSous.Repositories.Marten.Runtime.Saga;

public sealed class KyrolusMartenSagaEnvelope
{
    public Guid Id { get; set; }
    public string? Type { get; set; }
    public string? Payload { get; set; }
    public bool Completed { get; set; }
    public DateTime UpdatedAt { get; set; }
}
