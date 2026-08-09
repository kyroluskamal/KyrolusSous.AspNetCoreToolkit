using System;
using System.Collections.Generic;

namespace KyrolusSous.DataProtection.AwsKms;

public sealed class KyrolusAwsKmsOptions
{
    public string KeyId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string>? EncryptionContext { get; set; }
}
