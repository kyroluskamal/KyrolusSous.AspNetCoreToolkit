namespace KyrolusSous.Repositories.EF.Abstractions.Pagination;

/// <summary>
/// Specifies the seek direction for cursor-based (keyset) pagination.
/// </summary>
public enum KyrolusKeysetDirection
{
    /// <summary>
    /// Seeks items occurring after the reference cursor (forward navigation / next page).
    /// </summary>
    Forward,

    /// <summary>
    /// Seeks items occurring before the reference cursor (backward navigation / previous page).
    /// </summary>
    Backward
}
