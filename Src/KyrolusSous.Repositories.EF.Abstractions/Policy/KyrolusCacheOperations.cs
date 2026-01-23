namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

[Flags]
public enum KyrolusCacheReadOperations
{
    None = 0,
    GetByIdAsync = 1 << 0,
    GetByIdCompiledAsync = 1 << 1,

    GetAllAsync = 1 << 2,
    GetAllCompiledAsync = 1 << 3,

    QuerySpecAsync = 1 << 4,
    PagedSpecAsync = 1 << 5,
    StreamAsync = 1 << 6,
    SafeDefaults = GetByIdAsync | GetByIdCompiledAsync | GetAllCompiledAsync,
    All = ~0
}
