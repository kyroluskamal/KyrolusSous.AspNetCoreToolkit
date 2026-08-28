using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace KyrolusSous.DataProtection.EntityFramework;

/// <summary>
/// Entity Framework Core value converter that encrypts and decrypts string properties using <see cref="IDataProtector"/>.
/// </summary>
public sealed class KyrolusEncryptedValueConverter : ValueConverter<string?, string?>
{
    public KyrolusEncryptedValueConverter(IDataProtector protector)
        : base(
            v => v == null ? null : protector.Protect(v),
            v => v == null ? null : protector.Unprotect(v))
    {
    }
}
