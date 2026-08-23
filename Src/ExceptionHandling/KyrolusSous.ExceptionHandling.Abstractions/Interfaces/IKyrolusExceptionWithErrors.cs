namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

public interface IKyrolusExceptionWithErrors
{
    IReadOnlyList<KyrolusErrorItem>? GetErrors();
}
