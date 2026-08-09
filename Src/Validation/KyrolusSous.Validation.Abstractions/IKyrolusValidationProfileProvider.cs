namespace KyrolusSous.Validation.Abstractions;

public interface IKyrolusValidationProfileProvider
{
    bool TryGetProfile(string name, out KyrolusValidationContext context);
}
