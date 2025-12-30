using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using System.Collections;

namespace KyrolusSous.Repositories.Marten.Abstractions.Validation;

public sealed class KyrolusMartenNoopValidation : IKyrolusMartenValidation
{
    public static readonly IKyrolusMartenValidation Instance = new KyrolusMartenNoopValidation();

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class KyrolusMartenDelegateValidation(Func<string, object?, CancellationToken, Task> validate) : IKyrolusMartenValidation
{
    private readonly Func<string, object?, CancellationToken, Task> validate = validate ?? throw new ArgumentNullException(nameof(validate));

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => validate(operation, payload, cancellationToken);
}

public sealed class KyrolusMartenPayloadNotNullValidation(string? message = null) : IKyrolusMartenValidation
{
    private readonly string? message = message;

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (payload is not null) return Task.CompletedTask;
        throw new KyrolusMartenValidationException(message ?? "Payload is required.");
    }
}

public sealed class KyrolusMartenPayloadTypeValidation : IKyrolusMartenValidation
{
    private readonly HashSet<Type> allowed;
    private readonly bool allowNull;
    private readonly bool allowDerived;

    public KyrolusMartenPayloadTypeValidation(IEnumerable<Type> allowedTypes, bool allowNull = true, bool allowDerived = true)
    {
        ArgumentNullException.ThrowIfNull(allowedTypes);
        allowed = [.. allowedTypes];
        this.allowNull = allowNull;
        this.allowDerived = allowDerived;
    }

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null)
        {
            if (allowNull) return Task.CompletedTask;
            throw new KyrolusMartenValidationException("Payload is required.");
        }

        var type = payload.GetType();
        var match = allowDerived
            ? allowed.Any(t => t.IsAssignableFrom(type))
            : allowed.Contains(type);

        if (!match)
        {
            throw new KyrolusMartenValidationException($"Payload type '{type.FullName}' is not allowed.");
        }

        return Task.CompletedTask;
    }
}

public sealed class KyrolusMartenStringLengthValidation(int? minLength = null, int? maxLength = null, bool allowNonString = true) : IKyrolusMartenValidation
{
    private readonly int? minLength = minLength;
    private readonly int? maxLength = maxLength;
    private readonly bool allowNonString = allowNonString;

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null) return Task.CompletedTask;
        if (payload is not string s)
        {
            if (allowNonString) return Task.CompletedTask;
            throw new KyrolusMartenValidationException("Payload must be a string.");
        }

        if (minLength.HasValue && s.Length < minLength.Value)
            throw new KyrolusMartenValidationException($"String length must be at least {minLength.Value}.");
        if (maxLength.HasValue && s.Length > maxLength.Value)
            throw new KyrolusMartenValidationException($"String length must be at most {maxLength.Value}.");

        return Task.CompletedTask;
    }
}

public sealed class KyrolusMartenCollectionCountValidation(int? minCount = null, int? maxCount = null, bool requireCollection = false) : IKyrolusMartenValidation
{
    private readonly int? minCount = minCount;
    private readonly int? maxCount = maxCount;
    private readonly bool requireCollection = requireCollection;

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null) return Task.CompletedTask;

        if (!TryGetCount(payload, out var count))
        {
            if (requireCollection) throw new KyrolusMartenValidationException("Payload must be a collection.");
            return Task.CompletedTask;
        }

        if (minCount.HasValue && count < minCount.Value)
            throw new KyrolusMartenValidationException($"Collection count must be at least {minCount.Value}.");
        if (maxCount.HasValue && count > maxCount.Value)
            throw new KyrolusMartenValidationException($"Collection count must be at most {maxCount.Value}.");

        return Task.CompletedTask;
    }

    private static bool TryGetCount(object payload, out int count)
    {
        if (payload is ICollection collection)
        {
            count = collection.Count;
            return true;
        }

        if (payload is IEnumerable enumerable)
        {
            count = 0;
            foreach (var _ in enumerable)
            {
                count++;
                if (count == int.MaxValue) break;
            }
            return true;
        }

        count = 0;
        return false;
    }
}

public sealed class KyrolusMartenValidatablePayloadValidation(bool requireValidatable = false) : IKyrolusMartenValidation
{
    private readonly bool requireValidatable = requireValidatable;

    public async Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (payload is null) return;

        if (payload is IKyrolusMartenAsyncValidatable asyncValidatable)
        {
            await asyncValidatable.ValidateAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        if (payload is IKyrolusMartenValidatable validatable)
        {
            validatable.Validate();
            return;
        }

        if (requireValidatable)
            throw new KyrolusMartenValidationException("Payload does not implement a validation contract.");
    }
}

public sealed class KyrolusMartenOperationMapValidation(IReadOnlyDictionary<string, IKyrolusMartenValidation> map, IKyrolusMartenValidation? fallback = null) : IKyrolusMartenValidation
{
    private readonly IReadOnlyDictionary<string, IKyrolusMartenValidation> map = map ?? throw new ArgumentNullException(nameof(map));
    private readonly IKyrolusMartenValidation fallback = fallback ?? KyrolusMartenNoopValidation.Instance;

    public Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(operation)) return fallback.ValidateAsync(operation, payload, cancellationToken);
        if (map.TryGetValue(operation, out var validation)) return validation.ValidateAsync(operation, payload, cancellationToken);
        return fallback.ValidateAsync(operation, payload, cancellationToken);
    }
}

public sealed class KyrolusMartenCompositeValidation(IEnumerable<IKyrolusMartenValidation> validations, bool stopOnFirst = false) : IKyrolusMartenValidation
{
    private readonly IKyrolusMartenValidation[] validations = validations?.ToArray() ?? throw new ArgumentNullException(nameof(validations));
    private readonly bool stopOnFirst = stopOnFirst;

    public async Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (validations.Length == 0) return;

        if (stopOnFirst)
        {
            foreach (var validation in validations)
            {
                await validation.ValidateAsync(operation, payload, cancellationToken).ConfigureAwait(false);
            }
            return;
        }

        List<Exception>? errors = null;
        foreach (var validation in validations)
        {
            try
            {
                await validation.ValidateAsync(operation, payload, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                errors ??= [];
                errors.Add(ex);
            }
        }

        if (errors is not null)
        {
            throw new KyrolusMartenAggregateValidationException(errors);
        }
    }
}
