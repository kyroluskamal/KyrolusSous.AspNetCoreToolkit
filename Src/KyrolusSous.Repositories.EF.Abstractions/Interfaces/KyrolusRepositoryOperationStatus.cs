namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public enum KyrolusRepositoryOperationStatus
{
    Success,
    NotFound,
    ConcurrencyConflict,
    Failed
}

public readonly struct ConcurrencyInfo(byte[]? originalRowVersion, byte[]? currentRowVersion = null, IReadOnlyDictionary<string, object?>? databaseValues = null, int? retryCount = null)
{
    public byte[]? OriginalRowVersion { get; } = originalRowVersion;
    public byte[]? CurrentRowVersion { get; } = currentRowVersion;
    public IReadOnlyDictionary<string, object?>? DatabaseValues { get; } = databaseValues;
    public int? RetryCount { get; } = retryCount;
}

public readonly struct RepositoryOperationResult<TResult>(KyrolusRepositoryOperationStatus status, TResult? value = default, Exception? exception = null, bool pendingSave = false, ConcurrencyInfo? concurrency = null)
{

    public KyrolusRepositoryOperationStatus Status { get; } = status;
    public TResult? Value { get; } = value;
    public Exception? Exception { get; } = exception;
    public bool PendingSave { get; } = pendingSave;
    public ConcurrencyInfo? Concurrency { get; } = concurrency;

    public static RepositoryOperationResult<TResult> Success(TResult value, bool pendingSave = false, ConcurrencyInfo? concurrency = null) =>
        new(KyrolusRepositoryOperationStatus.Success, value, null, pendingSave, concurrency);

    public static RepositoryOperationResult<TResult> NotFound() => new(KyrolusRepositoryOperationStatus.NotFound);

    public static RepositoryOperationResult<TResult> ConcurrencyConflict(Exception? ex = null, ConcurrencyInfo? concurrency = null) =>
        new(KyrolusRepositoryOperationStatus.ConcurrencyConflict, default, ex, pendingSave: false, concurrency: concurrency);

    public static RepositoryOperationResult<TResult> Failed(Exception ex) =>
        new(KyrolusRepositoryOperationStatus.Failed, default, ex);

    public static RepositoryOperationResult<TResult> Pending(TResult value, ConcurrencyInfo? concurrency = null) =>
        new(KyrolusRepositoryOperationStatus.Success, value, null, pendingSave: true, concurrency: concurrency);
}
