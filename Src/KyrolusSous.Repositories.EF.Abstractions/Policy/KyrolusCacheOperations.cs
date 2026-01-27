namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

[Flags]
public enum KyrolusCacheReadOperations
{
    None = 0,
    GetByIdAsync = 1 << 0,
    GetByIdCompiledAsync = 1 << 1,

    GetAllAsync = 1 << 2,
    GetAllCompiledAsync = 1 << 3,

    GetAllIncludingDeletedAsync = 1 << 7,
    GetDeletedOnlyAsync = 1 << 8,
    GetByIdIncludingDeletedAsync = 1 << 9,
    SafeDefaults = GetByIdAsync | GetByIdCompiledAsync | GetAllCompiledAsync,
    All = ~0
}
