using System.Text.Json.Serialization;

namespace KyrolusSous.EndpointKit.Core.Batch;

/// <summary>
/// Batch operation types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KyrolusBatchOperationType
{
    /// <summary>Create a new entity.</summary>
    Create,

    /// <summary>Update an existing entity (full replacement).</summary>
    Update,

    /// <summary>Patch an existing entity (partial update).</summary>
    Patch,

    /// <summary>Delete an entity.</summary>
    Delete,

    /// <summary>Upsert - create if not exists, update if exists.</summary>
    Upsert
}

/// <summary>
/// Represents a single operation within a batch request.
/// </summary>
/// <typeparam name="TModel">The entity model type.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public class KyrolusBatchOperation<TModel, TKey>
{
    /// <summary>Unique identifier for this operation within the batch.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; set; }

    /// <summary>The type of operation to perform.</summary>
    [JsonPropertyName("operation")]
    public KyrolusBatchOperationType Operation { get; set; }

    /// <summary>The entity ID (required for Update, Patch, Delete).</summary>
    [JsonPropertyName("id")]
    public TKey? Id { get; set; }

    /// <summary>The entity data (required for Create, Update, Patch, Upsert).</summary>
    [JsonPropertyName("data")]
    public TModel? Data { get; set; }

    /// <summary>Continue processing remaining operations if this one fails (default: false).</summary>
    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; }
}

/// <summary>
/// Batch request containing multiple operations.
/// </summary>
/// <typeparam name="TModel">The entity model type.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public class KyrolusBatchRequest<TModel, TKey>
{
    /// <summary>The operations to execute.</summary>
    [JsonPropertyName("operations")]
    public IReadOnlyList<KyrolusBatchOperation<TModel, TKey>> Operations { get; set; } = Array.Empty<KyrolusBatchOperation<TModel, TKey>>();

    /// <summary>Execute all operations in a single transaction (default: true).</summary>
    [JsonPropertyName("atomic")]
    public bool Atomic { get; set; } = true;

    /// <summary>Continue processing remaining operations if one fails (default: false). Ignored if Atomic is true.</summary>
    [JsonPropertyName("continueOnError")]
    public bool ContinueOnError { get; set; }

    /// <summary>Return full entity data in responses (default: true).</summary>
    [JsonPropertyName("returnData")]
    public bool ReturnData { get; set; } = true;
}

/// <summary>
/// Result of a single batch operation.
/// </summary>
/// <typeparam name="TResponse">The response entity type.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public class KyrolusBatchOperationResult<TResponse, TKey>
{
    /// <summary>The operation ID from the request.</summary>
    [JsonPropertyName("operationId")]
    public string? OperationId { get; set; }

    /// <summary>The operation type that was executed.</summary>
    [JsonPropertyName("operation")]
    public KyrolusBatchOperationType Operation { get; set; }

    /// <summary>The entity ID.</summary>
    [JsonPropertyName("id")]
    public TKey? Id { get; set; }

    /// <summary>Whether the operation succeeded.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>HTTP status code for this operation.</summary>
    [JsonPropertyName("status")]
    public int Status { get; set; }

    /// <summary>The resulting entity data (if requested and successful).</summary>
    [JsonPropertyName("data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TResponse? Data { get; set; }

    /// <summary>Error information if the operation failed.</summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public KyrolusBatchError? Error { get; set; }

    /// <summary>Creates a success result.</summary>
    public static KyrolusBatchOperationResult<TResponse, TKey> Succeeded(
        string? operationId,
        KyrolusBatchOperationType operation,
        TKey? id,
        int status,
        TResponse? data = default)
    {
        return new KyrolusBatchOperationResult<TResponse, TKey>
        {
            OperationId = operationId,
            Operation = operation,
            Id = id,
            Success = true,
            Status = status,
            Data = data
        };
    }

    /// <summary>Creates a failure result.</summary>
    public static KyrolusBatchOperationResult<TResponse, TKey> Failed(
        string? operationId,
        KyrolusBatchOperationType operation,
        TKey? id,
        int status,
        string errorCode,
        string errorMessage,
        IReadOnlyList<KyrolusBatchErrorDetail>? details = null)
    {
        return new KyrolusBatchOperationResult<TResponse, TKey>
        {
            OperationId = operationId,
            Operation = operation,
            Id = id,
            Success = false,
            Status = status,
            Error = new KyrolusBatchError(errorCode, errorMessage, details)
        };
    }
}

/// <summary>
/// Error information for a failed batch operation.
/// </summary>
public class KyrolusBatchError
{
    public KyrolusBatchError() { }

    public KyrolusBatchError(string code, string message, IReadOnlyList<KyrolusBatchErrorDetail>? details = null)
    {
        Code = code;
        Message = message;
        Details = details;
    }

    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    /// <summary>Human-readable error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    /// <summary>Detailed error information.</summary>
    [JsonPropertyName("details")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<KyrolusBatchErrorDetail>? Details { get; set; }
}

/// <summary>
/// Detailed error information for a batch operation.
/// </summary>
public class KyrolusBatchErrorDetail
{
    public KyrolusBatchErrorDetail() { }

    public KyrolusBatchErrorDetail(string? field, string code, string message)
    {
        Field = field;
        Code = code;
        Message = message;
    }

    /// <summary>Field that caused the error.</summary>
    [JsonPropertyName("field")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Field { get; set; }

    /// <summary>Error code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; set; } = default!;

    /// <summary>Error message.</summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;
}

/// <summary>
/// Response from a batch operation request.
/// </summary>
/// <typeparam name="TResponse">The response entity type.</typeparam>
/// <typeparam name="TKey">The entity key type.</typeparam>
public class KyrolusBatchResponse<TResponse, TKey>
{
    /// <summary>Overall success status.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>Total number of operations in the request.</summary>
    [JsonPropertyName("totalOperations")]
    public int TotalOperations { get; set; }

    /// <summary>Number of successful operations.</summary>
    [JsonPropertyName("successCount")]
    public int SuccessCount { get; set; }

    /// <summary>Number of failed operations.</summary>
    [JsonPropertyName("failureCount")]
    public int FailureCount { get; set; }

    /// <summary>Results for each operation.</summary>
    [JsonPropertyName("results")]
    public IReadOnlyList<KyrolusBatchOperationResult<TResponse, TKey>> Results { get; set; } = Array.Empty<KyrolusBatchOperationResult<TResponse, TKey>>();

    /// <summary>Creates a batch response from operation results.</summary>
    public static KyrolusBatchResponse<TResponse, TKey> FromResults(
        IReadOnlyList<KyrolusBatchOperationResult<TResponse, TKey>> results)
    {
        var successCount = results.Count(r => r.Success);
        return new KyrolusBatchResponse<TResponse, TKey>
        {
            Success = results.All(r => r.Success),
            TotalOperations = results.Count,
            SuccessCount = successCount,
            FailureCount = results.Count - successCount,
            Results = results
        };
    }
}

/// <summary>
/// Configuration options for batch operations.
/// </summary>
public class KyrolusBatchOptions
{
    /// <summary>Enable batch operations endpoint (default: false).</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>Maximum number of operations per batch request (default: 100).</summary>
    public int MaxOperationsPerBatch { get; set; } = 100;

    /// <summary>Default atomic mode (default: true).</summary>
    public bool DefaultAtomic { get; set; } = true;

    /// <summary>Allow non-atomic batches (default: true).</summary>
    public bool AllowNonAtomic { get; set; } = true;

    /// <summary>Enable parallel execution for non-atomic batches (default: false).</summary>
    public bool EnableParallelExecution { get; set; } = false;

    /// <summary>Maximum parallelism for parallel execution (default: 4).</summary>
    public int MaxParallelism { get; set; } = 4;

    /// <summary>Endpoint route suffix (default: "$batch").</summary>
    public string RouteSuffix { get; set; } = "$batch";

    /// <summary>Allowed operation types (default: all).</summary>
    public HashSet<KyrolusBatchOperationType> AllowedOperations { get; set; } = new()
    {
        KyrolusBatchOperationType.Create,
        KyrolusBatchOperationType.Update,
        KyrolusBatchOperationType.Patch,
        KyrolusBatchOperationType.Delete,
        KyrolusBatchOperationType.Upsert
    };
}
