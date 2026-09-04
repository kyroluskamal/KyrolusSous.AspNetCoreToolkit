namespace KyrolusSous.CQRS.Abstractions.Outbox;

/// <summary>
/// Default <see cref="IKyrolusOutboxEventTypeRegistry"/>: an explicit allow-list built once from a
/// fixed set of types, keyed by <see cref="Type.FullName"/>.
/// </summary>
public sealed class KyrolusOutboxEventTypeRegistry : IKyrolusOutboxEventTypeRegistry
{
    private readonly IReadOnlyDictionary<string, Type> _byQualifiedOrFullName;
    private readonly IReadOnlyDictionary<string, Type> _byShortName;

    public KyrolusOutboxEventTypeRegistry(IEnumerable<Type> allowedEventTypes)
    {
        ArgumentNullException.ThrowIfNull(allowedEventTypes);

        // Indexed under every name an outbox producer might have stored: KyrolusOutboxExtensions and
        // KyrolusMartenOutboxExtensions (in KyrolusSous.Repositories.*) both write
        // AssemblyQualifiedName first, falling back to FullName then Name only if the type somehow
        // has neither - so all three have to resolve here for messages already written that way to
        // still be processable.
        //
        // The bare Name is the only one of the three that is not inherently unique - two
        // notification types in different namespaces can share a short name - so unlike
        // AssemblyQualifiedName/FullName it needs a collision check: silently letting a later type
        // overwrite an earlier one under the same short Name would mean a message resolved by that
        // name deserializes into whichever type happened to be enumerated last (assembly load order,
        // not anything meaningful), with no error. A name involved in a collision is dropped from the
        // map entirely instead - a message stored under that bare name fails to resolve rather than
        // resolving to the wrong type; it still resolves fine via its AssemblyQualifiedName/FullName.
        //
        // Kept in a dictionary separate from the qualified/full-name map: a top-level type's short
        // Name and FullName are the same literal string (no namespace to prefix), so writing that
        // type's FullName into a shared map could silently overwrite a *different* type's legitimate
        // short-name entry at the very same key - and since that overwrite happens before this type's
        // own short-name collision check runs, the check would find its own just-written entry and
        // conclude (wrongly) that no collision occurred, erasing the ambiguity trail. Two dictionaries
        // means a FullName write can never clobber short-name bookkeeping.
        var byQualifiedOrFullName = new Dictionary<string, Type>(StringComparer.Ordinal);
        var byShortName = new Dictionary<string, Type>(StringComparer.Ordinal);
        var ambiguousNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var type in allowedEventTypes)
        {
            if (type.AssemblyQualifiedName is { } aqn) byQualifiedOrFullName[aqn] = type;
            if (type.FullName is { } fullName) byQualifiedOrFullName[fullName] = type;

            if (ambiguousNames.Contains(type.Name)) continue;

            if (byShortName.TryGetValue(type.Name, out var existingByShortName) && existingByShortName != type)
            {
                byShortName.Remove(type.Name);
                ambiguousNames.Add(type.Name);
                continue;
            }

            byShortName[type.Name] = type;
        }

        _byQualifiedOrFullName = byQualifiedOrFullName;
        _byShortName = byShortName;
    }

    /// <summary>
    /// Builds a registry from every concrete <see cref="IKyrolusNotification"/> found in the given
    /// assemblies - a much narrower surface than "every type in every loaded assembly", but still
    /// zero-configuration for the common case where outbox events are the application's own
    /// notification types.
    /// </summary>
    public static KyrolusOutboxEventTypeRegistry FromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        var notificationInterface = typeof(IKyrolusNotification);
        var types = assemblies
            .SelectMany(GetLoadableTypes)
            .Where(type => type is { IsClass: true, IsAbstract: false } && notificationInterface.IsAssignableFrom(type));

        return new KyrolusOutboxEventTypeRegistry(types);
    }

    /// <inheritdoc />
    public bool TryResolve(string eventTypeName, out Type? eventType)
        => _byQualifiedOrFullName.TryGetValue(eventTypeName, out eventType)
        || _byShortName.TryGetValue(eventTypeName, out eventType);

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return exception.Types.Where(type => type is not null)!;
        }
    }
}
