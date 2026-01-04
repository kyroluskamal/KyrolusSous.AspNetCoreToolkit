namespace KyrolusSous.DataProtection.Redis;

public sealed class KyrolusRedisKeyRingRefreshOptions
{
    public string Channel { get; set; } = "kyrolus:dataprotection:keyring";
    public bool IncludeApplicationNameInChannel { get; set; } = true;
}
