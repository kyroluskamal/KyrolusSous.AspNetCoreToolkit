namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

public sealed class KyrolusErrorCodeRegistryException(string message) : Exception(message)
{
}
