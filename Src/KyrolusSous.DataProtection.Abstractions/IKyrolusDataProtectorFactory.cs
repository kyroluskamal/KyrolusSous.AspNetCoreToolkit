using Microsoft.AspNetCore.DataProtection;

namespace KyrolusSous.DataProtection.Abstractions;

public interface IKyrolusDataProtectorFactory
{
    IDataProtector CreateProtector(string purpose);
}
