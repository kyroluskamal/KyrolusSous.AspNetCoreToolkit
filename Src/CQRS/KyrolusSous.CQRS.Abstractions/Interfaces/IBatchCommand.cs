namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Defines a batch CQRS command that processes multiple items in a single operation.
/// </summary>
/// <typeparam name="TItem">The type of each item in the batch.</typeparam>
/// <typeparam name="TResponse">The response type returned by the batch handler.</typeparam>
public interface IKyrolusBatchCommand<out TItem, out TResponse> : IKyrolusCommand<TResponse>
{
    /// <summary>
    /// Gets the collection of items to process in this batch.
    /// </summary>
    IReadOnlyList<TItem> Items { get; }
}

/// <summary>
/// Defines a batch CQRS command with no response payload.
/// </summary>
/// <typeparam name="TItem">The type of each item in the batch.</typeparam>
public interface IKyrolusBatchCommand<out TItem> : IKyrolusCommand
{
    /// <summary>
    /// Gets the collection of items to process in this batch.
    /// </summary>
    IReadOnlyList<TItem> Items { get; }
}

/// <summary>
/// Defines a batch CQRS query that retrieves multiple items by their keys.
/// </summary>
/// <typeparam name="TKey">The key type for each item.</typeparam>
/// <typeparam name="TItem">The retrieved item type.</typeparam>
public interface IKyrolusBatchQuery<out TKey, out TItem> : IKyrolusQuery<IReadOnlyList<TItem>>
{
    /// <summary>
    /// Gets the list of keys to query.
    /// </summary>
    IReadOnlyList<TKey> Keys { get; }
}
