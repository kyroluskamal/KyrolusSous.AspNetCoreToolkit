namespace KyrolusSous.RateLimiting;

/// <summary>
/// Artificial Intelligence, Large Language Model identifiers, and generative token streaming partitioning strategies.
/// </summary>
public static partial class KyrolusRateLimitPartitionKeys
{
    /// <summary>
    /// Resolves a partition key based on the requested Artificial Intelligence / Large Language Model identifier (e.g. from <c>"X-AI-Model"</c> header or <c>"model"</c> query parameter).
    /// Prevents GPU VRAM exhaustion, token quota abuse, and noisy neighbor interference on costly LLM reasoning models.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The AI model partition key formatted as <c>"aimodel_{model}"</c>, or <c>"aimodel_unspecified"</c>.</returns>
    public static string GetAiModelKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? model = null;
        if (context.Request.Headers.TryGetValue("X-AI-Model", out var headerVal) && !string.IsNullOrWhiteSpace(headerVal))
        {
            model = headerVal.ToString();
        }
        else if (context.Request.Query.TryGetValue("model", out var queryVal) && !string.IsNullOrWhiteSpace(queryVal))
        {
            model = queryVal.ToString();
        }

        if (string.IsNullOrWhiteSpace(model))
            return "aimodel_unspecified";

        var sanitized = model.Trim().ToLowerInvariant().Replace('/', '_').Replace(':', '_').Replace(' ', '_');
        return $"aimodel_{sanitized}";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI model key with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{aiModelKey}:{clientKey}"</c>.</returns>
    public static string GetAiModelAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var modelKey = GetAiModelKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{modelKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the Artificial Intelligence execution mode (Server-Sent Events streaming vs unary completion).
    /// Detects streaming requests via <c>"stream=true"</c> query parameter, <c>"Accept: text/event-stream"</c>, or <c>"X-AI-Stream: true"</c>.
    /// Protects gateway connection pools from prolonged socket starvation caused by concurrent token streaming.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns><c>"aistream_streaming"</c> if streaming is requested; otherwise <c>"aistream_unary"</c>.</returns>
    public static string GetAiStreamModeKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Query.TryGetValue("stream", out var streamQuery) &&
            (string.Equals(streamQuery, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(streamQuery, "1", StringComparison.OrdinalIgnoreCase)))
        {
            return "aistream_streaming";
        }

        if (context.Request.Headers.TryGetValue("X-AI-Stream", out var streamHeader) &&
            (string.Equals(streamHeader, "true", StringComparison.OrdinalIgnoreCase) || string.Equals(streamHeader, "1", StringComparison.OrdinalIgnoreCase)))
        {
            return "aistream_streaming";
        }

        if (context.Request.Headers.TryGetValue("Accept", out var acceptVal) &&
            acceptVal.ToString().Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            return "aistream_streaming";
        }

        return "aistream_unary";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI streaming mode with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{streamModeKey}:{clientKey}"</c>.</returns>
    public static string GetAiStreamModeAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var streamModeKey = GetAiStreamModeKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{streamModeKey}:{clientKey}";
    }

    private static readonly string[] DefaultAiTokenHeaders =
    [
        "X-AI-Prompt-Tokens",
        "X-Prompt-Tokens",
        "X-Context-Tokens",
        "X-Context-Length"
    ];

    /// <summary>
    /// Resolves a partition key based on the declared or estimated AI prompt token context size (<c>"ai_tokens_short"</c>, <c>"ai_tokens_medium"</c>, <c>"ai_tokens_massive"</c>).
    /// Protects backend LLM inference clusters (vLLM, Ollama, TensorRT-LLM) from GPU KV-Cache Out-Of-Memory exhaustion by segregating heavy 128k+ context queries from lightweight queries.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="shortThreshold">Threshold up to which a prompt context is categorized as short. Defaults to 4,096 tokens.</param>
    /// <param name="mediumThreshold">Threshold up to which a prompt context is categorized as medium. Defaults to 32,768 tokens.</param>
    /// <param name="tokenHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>The AI token context bucket key.</returns>
    public static string GetAiTokenContextBucketKey(
        HttpContext context,
        int shortThreshold = 4096,
        int mediumThreshold = 32768,
        string[]? tokenHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(shortThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThan(mediumThreshold, shortThreshold);

        var candidates = tokenHeaderCandidates is { Length: > 0 } ? tokenHeaderCandidates : DefaultAiTokenHeaders;
        foreach (var header in candidates)
        {
            if (context.Request.Headers.TryGetValue(header, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                if (int.TryParse(val.ToString().Trim(), CultureInfo.InvariantCulture, out var tokens))
                {
                    if (tokens <= shortThreshold)
                        return "ai_tokens_short";
                    if (tokens <= mediumThreshold)
                        return "ai_tokens_medium";
                    return "ai_tokens_massive";
                }
            }
        }

        return "ai_tokens_short";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI token context bucket with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="shortThreshold">Threshold up to which a prompt context is categorized as short.</param>
    /// <param name="mediumThreshold">Threshold up to which a prompt context is categorized as medium.</param>
    /// <param name="tokenHeaderCandidates">Optional prioritized list of header names.</param>
    /// <returns>A compound key formatted as <c>"{tokenBucket}:{clientKey}"</c>.</returns>
    public static string GetAiTokenContextBucketAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int shortThreshold = 4096,
        int mediumThreshold = 32768,
        string[]? tokenHeaderCandidates = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bucket = GetAiTokenContextBucketKey(context, shortThreshold, mediumThreshold, tokenHeaderCandidates);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{bucket}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the AI task capability family (<c>"ai_task_embeddings"</c>, <c>"ai_task_chat"</c>, <c>"ai_task_vision"</c>, <c>"ai_task_audio"</c>, <c>"ai_task_rerank"</c>, <c>"ai_task_general"</c>).
    /// Segregates high-frequency, low-latency embedding lookups from compute-heavy vision and audio generation workloads.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The AI task capability family partition key.</returns>
    public static string GetAiTaskCapabilityKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        string? task = null;
        if (context.Request.Headers.TryGetValue("X-AI-Task", out var tVal) && !string.IsNullOrWhiteSpace(tVal))
            task = tVal.ToString();
        else if (context.Request.Headers.TryGetValue("X-Task-Type", out var ttVal) && !string.IsNullOrWhiteSpace(ttVal))
            task = ttVal.ToString();
        else
        {
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            if (path.Contains("embed", StringComparison.Ordinal)) return "ai_task_embeddings";
            if (path.Contains("chat", StringComparison.Ordinal) || path.Contains("completion", StringComparison.Ordinal)) return "ai_task_chat";
            if (path.Contains("vision", StringComparison.Ordinal) || path.Contains("image", StringComparison.Ordinal)) return "ai_task_vision";
            if (path.Contains("audio", StringComparison.Ordinal) || path.Contains("speech", StringComparison.Ordinal) || path.Contains("transcription", StringComparison.Ordinal)) return "ai_task_audio";
            if (path.Contains("rerank", StringComparison.Ordinal)) return "ai_task_rerank";
        }

        if (!string.IsNullOrWhiteSpace(task))
        {
            var raw = task.Trim().ToLowerInvariant();
            if (raw.Contains("embed")) return "ai_task_embeddings";
            if (raw.Contains("chat") || raw.Contains("completion")) return "ai_task_chat";
            if (raw.Contains("vision") || raw.Contains("image")) return "ai_task_vision";
            if (raw.Contains("audio") || raw.Contains("speech")) return "ai_task_audio";
            if (raw.Contains("rerank")) return "ai_task_rerank";
            return $"ai_task_{SanitizeSegment(raw)}";
        }

        return "ai_task_general";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI task capability family with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{taskKey}:{clientKey}"</c>.</returns>
    public static string GetAiTaskCapabilityAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var taskKey = GetAiTaskCapabilityKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{taskKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on the declared Chain-of-Thought (CoT) reasoning effort (<c>"ai_reasoning_high"</c>, <c>"ai_reasoning_medium"</c>, <c>"ai_reasoning_low"</c>, <c>"ai_reasoning_none"</c>).
    /// Throttles expensive reasoning workloads (e.g. OpenAI o1/o3, DeepSeek-R1, Claude Thinking) that consume substantial inference computation.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>The reasoning effort partition key.</returns>
    public static string GetAiReasoningEffortKey(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Request.Headers.TryGetValue("X-AI-Reasoning-Effort", out var val) ||
            context.Request.Headers.TryGetValue("X-Reasoning-Effort", out val))
        {
            var raw = val.ToString().Trim().ToLowerInvariant();
            if (raw is "high" or "3") return "ai_reasoning_high";
            if (raw is "medium" or "2") return "ai_reasoning_medium";
            if (raw is "low" or "1") return "ai_reasoning_low";
        }

        return "ai_reasoning_none";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI reasoning effort with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <returns>A compound key formatted as <c>"{reasoningKey}:{clientKey}"</c>.</returns>
    public static string GetAiReasoningEffortAndClientKey(HttpContext context, Func<HttpContext, string>? clientKeyResolver = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var reasoningKey = GetAiReasoningEffortKey(context);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{reasoningKey}:{clientKey}";
    }

    /// <summary>
    /// Resolves a partition key based on autonomous AI agent execution recursion depth or turn count.
    /// Detects runaway agent execution loops where multi-agent frameworks (e.g. LangChain, AutoGen, Semantic Kernel) get caught in recursive tool-calling cascades.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="maxDepth">Maximum allowed recursion depth before flagging as dangerous loop. Defaults to 10.</param>
    /// <returns><c>"ai_agent_loop_risk"</c> if depth exceeds threshold; otherwise <c>"ai_agent_depth_{N}"</c> or <c>"ai_agent_direct"</c>.</returns>
    public static string GetAiAgentRecursionDepthKey(HttpContext context, int maxDepth = 10)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);

        if (context.Request.Headers.TryGetValue("X-Agent-Depth", out var dVal) ||
            context.Request.Headers.TryGetValue("X-Agent-Turn", out dVal) ||
            context.Request.Headers.TryGetValue("X-Recursion-Depth", out dVal))
        {
            if (int.TryParse(dVal.ToString().Trim(), CultureInfo.InvariantCulture, out var depth))
            {
                if (depth > maxDepth)
                    return "ai_agent_loop_risk";

                return $"ai_agent_depth_{depth}";
            }
        }

        return "ai_agent_direct";
    }

    /// <summary>
    /// Resolves a compound partition key combining the AI agent recursion depth with a client partition key.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <param name="clientKeyResolver">An optional function resolving the client's identity. If null, defaults to <see cref="GetClientIpKey(HttpContext)"/>.</param>
    /// <param name="maxDepth">Maximum allowed recursion depth.</param>
    /// <returns>A compound key formatted as <c>"{agentDepthKey}:{clientKey}"</c>.</returns>
    public static string GetAiAgentRecursionDepthAndClientKey(
        HttpContext context,
        Func<HttpContext, string>? clientKeyResolver = null,
        int maxDepth = 10)
    {
        ArgumentNullException.ThrowIfNull(context);

        var depthKey = GetAiAgentRecursionDepthKey(context, maxDepth);
        var clientKey = clientKeyResolver?.Invoke(context) ?? GetClientIpKey(context);
        return $"{depthKey}:{clientKey}";
    }
}
