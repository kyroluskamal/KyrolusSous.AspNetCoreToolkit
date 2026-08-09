using System;
using System.Diagnostics.CodeAnalysis;

namespace KyrolusSous.Repositories.EF.Abstractions;

public static class ExceptionExtension
{
    extension(ArgumentException)
    {
        public static void ThrowIfKeyValuesIsNotValid([NotNull] object?[]? keyValues, int expectedLength)
        {
            if (keyValues is not { Length: > 0 } || keyValues.Length != expectedLength)

                throw new ArgumentException($"Invalid number of key values provided. Expected {expectedLength}, got {keyValues?.Length ?? 0}.");
        }
        public static void ThrowIfUpdatesIsNotValid([NotNull] Dictionary<string, object>? updates)
        {
            if (updates is not { Count: > 0 })
                throw new ArgumentException("At least one property update must be provided.", nameof(updates));
        }
    }
}
