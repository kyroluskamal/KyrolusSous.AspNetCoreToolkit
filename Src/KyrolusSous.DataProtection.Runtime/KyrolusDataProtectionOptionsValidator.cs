using KyrolusSous.DataProtection.Abstractions;
using Microsoft.Extensions.Options;

namespace KyrolusSous.DataProtection.Runtime;

public sealed class KyrolusDataProtectionOptionsValidator : IValidateOptions<KyrolusDataProtectionOptions>
{
    public ValidateOptionsResult Validate(string? name, KyrolusDataProtectionOptions options)
    {
        if (options is null)
        {
            return ValidateOptionsResult.Fail("Kyrolus data protection options are missing.");
        }

        if (string.IsNullOrWhiteSpace(options.ApplicationName))
        {
            return ValidateOptionsResult.Fail("ApplicationName is required.");
        }

        var protection = options.KeyProtection;
        if (protection is null || protection.Kind == KyrolusKeyProtectionKind.None)
        {
            return ValidateOptionsResult.Success;
        }

        if (protection.Kind == KyrolusKeyProtectionKind.Dpapi && !OperatingSystem.IsWindows())
        {
            return ValidateOptionsResult.Fail("DPAPI key protection is only supported on Windows.");
        }

        if (protection.Kind == KyrolusKeyProtectionKind.Certificate &&
            string.IsNullOrWhiteSpace(protection.CertificateThumbprint))
        {
            return ValidateOptionsResult.Fail("CertificateThumbprint is required for certificate key protection.");
        }

        return ValidateOptionsResult.Success;
    }
}
