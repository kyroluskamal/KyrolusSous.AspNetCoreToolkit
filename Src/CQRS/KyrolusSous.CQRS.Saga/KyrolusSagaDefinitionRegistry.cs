namespace KyrolusSous.CQRS.Saga;

/// <summary>
/// Default <see cref="IKyrolusSagaDefinitionRegistry"/>: an allow-list built once from every
/// <see cref="IKyrolusSagaDefinition"/> registered in the container.
/// </summary>
public sealed class KyrolusSagaDefinitionRegistry : IKyrolusSagaDefinitionRegistry
{
    private readonly IReadOnlyDictionary<string, IKyrolusSagaDefinition> _byName;

    public KyrolusSagaDefinitionRegistry(IEnumerable<IKyrolusSagaDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);

        var map = new Dictionary<string, IKyrolusSagaDefinition>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (map.ContainsKey(definition.SagaName))
                throw new InvalidOperationException(
                    $"[Kyrolus Saga] Two saga definitions are registered under the name '{definition.SagaName}'. " +
                    "SagaName must be unique - a stored instance's name is the only way to find its definition again on resume.");

            map[definition.SagaName] = definition;
        }

        _byName = map;
    }

    /// <inheritdoc />
    public bool TryGet(string sagaName, out IKyrolusSagaDefinition? definition)
        => _byName.TryGetValue(sagaName, out definition);
}
