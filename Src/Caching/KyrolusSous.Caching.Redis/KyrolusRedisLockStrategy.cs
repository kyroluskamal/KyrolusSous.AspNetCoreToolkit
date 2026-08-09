namespace KyrolusSous.Caching.Redis;

public enum KyrolusRedisLockStrategy
{
    Lua = 1,
    Simple = 2,
    Disabled = 3
}
