namespace KyrolusSous.Repositories.Marten.Abstractions.Pagination;

/// <summary>
/// Represents a keyset/cursor-based page of Marten documents for constant-time O(1) pagination.
/// </summary>
/// <typeparam name="T">Document type.</typeparam>
/// <typeparam name="TCursor">Cursor/Key type.</typeparam>
public sealed record MartenKeysetPage<T, TCursor>(
    IReadOnlyList<T> Items,
    bool HasNext,
    TCursor? NextCursor);
